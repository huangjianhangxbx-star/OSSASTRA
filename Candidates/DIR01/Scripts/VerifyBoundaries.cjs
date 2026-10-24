/* DIR01 targeted boundary evidence. Uses existing local runtime/browser only. */
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const {chromium}=require(process.argv[2]),study=path.resolve(process.argv[3]);
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
 const page=await browser.newPage({viewport:{width:1500,height:1100}}),errors=[];
 page.on('pageerror',e=>errors.push(e.message));await page.goto('http://127.0.0.1:5187');await page.waitForFunction(()=>window.dir01?.ready);
 const runtime=await page.evaluate(()=>{
  const a=window.dir01,rows=[],cases=[],faces=['上','下','左','左上','左下'];a.setRotating(false);a.setMode('E');a.setOptions({mix:0,cull:true,zoom:'detail'});
  for(const action of ['_stand','_move','a1','a2','_dash','skill_飞盾2'])for(const progress of [0,.25,.5,.75,.999])for(const face of faces)for(const mirror of [false,true]){
   a.startAction(action);a.seek(progress);a.setDirection(face,mirror);a.pose(0);const p=a.snapshot();
   cases.push({action,progress,face,mirror,finite:p.hands.every(h=>Number.isFinite(h.x)&&Number.isFinite(h.y)),selectedRoot:p.branches.find(b=>b.name===face),render:p.render.drawnAttachments});
  }
  rows.push({name:'六动作×五进度×五面×镜像均完成真实求解',ok:cases.length===300&&cases.every(p=>p.finite)});
  const clipping=[];for(const action of ['_stand','_out','_出井盖','_跳井盖'])for(const progress of [0,.25,.5,.75,.999]){a.startAction(action);a.seek(progress);a.pose(0);clipping.push({action,progress,scaleAttachment:a.skeleton.findSlot('scale')?.getAttachment()?.name||null,maskIndex:a.snapshot().render.maskDrawIndex});}
  rows.push({name:'scale起始裁切由实际动作启用',ok:clipping.some(p=>p.action!=='_stand'&&p.scaleAttachment)&&clipping.filter(p=>p.action==='_stand').every(p=>!p.scaleAttachment)});
  a.startAction('a1');a.seek(.5);a.setDirection('左',false);a.setOptions({constraints:true});const on=a.snapshot();a.setOptions({constraints:false});const off=a.snapshot();
  const drift=on.hands.map((h,i)=>({name:h.name,branch:h.branch,distance:Math.hypot(h.x-off.hands[i].x,h.y-off.hands[i].y)}));
  rows.push({name:'关闭源约束改变真实手骨结果',ok:drift.some(d=>d.distance>.01)});a.setOptions({constraints:true});const restored=a.snapshot();
  rows.push({name:'约束开关恢复原姿态',ok:on.hands.every((h,i)=>Math.hypot(h.x-restored.hands[i].x,h.y-restored.hands[i].y)<.001)});
  const edge=[];let inspectedAttachments=0,invalidVertices=0;for(const action of ['a4.5丢矛','skill_飞盾2','skill_大戳+'])for(const progress of [0,.25,.5,.75,.999]){a.startAction(action);a.seek(progress);a.pose(0);const r=a.snapshot().render.clipRect;for(const slot of a.skeleton.slots){const att=slot.getAttachment();if(!(att instanceof a.s.RegionAttachment||att instanceof a.s.MeshAttachment))continue;const length=att instanceof a.s.RegionAttachment?8:att.worldVerticesLength,v=new Float32Array(length);if(att instanceof a.s.RegionAttachment)att.computeWorldVertices(slot.bone,v,0,2);else att.computeWorldVertices(slot,0,length,v,0,2);inspectedAttachments++;if([...v].some(x=>!Number.isFinite(x)))invalidVertices++;const originInside=slot.bone.worldX>=r.minX&&slot.bone.worldX<r.maxX&&slot.bone.worldY>=r.minY&&slot.bone.worldY<r.maxY;const verticesOutside=[...v].some((x,i)=>i%2===0?(x<r.minX||x>=r.maxX):(x<r.minY||x>=r.maxY));if(originInside&&verticesOutside)edge.push({action,progress,slot:slot.data.name,branch:a.branch(slot.bone)});}}
  // Zero observed crossings is a valid model result, not a reason to tune the mask.
  // The two algorithms differ by definition/IL; do not fabricate a visual failure.
  rows.push({name:'15个长兵器动作采样已完成边缘检查',ok:inspectedAttachments>0&&invalidVertices===0,inspectedAttachments,invalidVertices,observedCrossings:edge.length,limitation:'仅这15个动作时点；不代表全部动画均不越界'});
  const jitter=[22.49,22.51,22.49,22.51].map(angle=>({angle,...a.originalSector(angle)}));rows.push({name:'无滞回选面在边界来回切换',ok:jitter.every((x,i)=>i===0||x.face!==jitter[i-1].face)});
  return{rows,cases,clipping,constraintHandDrift:drift,edgeCrossings:edge,jitter,failed:rows.filter(r=>!r.ok).length};
 });
 fs.writeFileSync(path.join(study,'evidence/boundaries-local.json'),JSON.stringify(runtime,null,2));
 const atlas=fs.readFileSync(path.join(study,'assets/unit.atlas'),'utf8').split(/\r?\n/),regions=[];let region=null;
 for(let i=1;i<atlas.length;i++){const line=atlas[i].trim();if(!line)continue;if(!line.includes(':')){region={name:line};regions.push(region);}else if(region){const split=line.indexOf(':');region[line.slice(0,split)]=line.slice(split+1).split(',').map(Number);}}
 const images=regions.map(r=>{const file=path.join(study,'recovered/images',r.name+'.png');if(!fs.existsSync(file))return{name:r.name,exists:false};const b=fs.readFileSync(file),actual=[b.readUInt32BE(16),b.readUInt32BE(20)],expected=r.offsets?r.offsets.slice(2):r.bounds.slice(2);return{name:r.name,exists:true,actual,expected,match:actual.every((n,i)=>n===expected[i]),rotated:r.rotate?.[0]===90};});
 const manifest=JSON.parse(fs.readFileSync(path.join(study,'assets-manifest.json'),'utf8').replace(/^\uFEFF/,''));
 const hashes=manifest.map(x=>({name:x.name,unchanged:crypto.createHash('sha256').update(fs.readFileSync(x.source)).digest('hex').toUpperCase()===x.sha}));
 fs.writeFileSync(path.join(study,'evidence/unpack-validation-local.json'),JSON.stringify({images,hashes},null,2));
 const summary={runtimeChecks:runtime.rows,poseSamples:runtime.cases.length,errors,unpack:{regions:images.length,rotated:images.filter(x=>x.rotated).length,missing:images.filter(x=>!x.exists),wrongDimensions:images.filter(x=>x.exists&&!x.match)},sourceHashes:hashes};
 fs.writeFileSync(process.argv[4],JSON.stringify(summary,null,2));await browser.close();console.log(JSON.stringify(summary));if(runtime.failed||errors.length||images.some(x=>!x.exists||!x.match)||hashes.some(x=>!x.unchanged))process.exitCode=1;
})().catch(e=>{console.error(e);process.exit(1)});
