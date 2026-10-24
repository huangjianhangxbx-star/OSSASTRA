/* DIR01只读结构清点：输出完整数据到本机研究目录，不输出/发布商业骨架副本。 */
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const [input,output]=process.argv.slice(2);if(!input||!output)throw Error('node AnalyzeRig.cjs <unit.json> <local-output.json>');
const d=JSON.parse(fs.readFileSync(input,'utf8')),byName=new Map(d.bones.map((b,i)=>[b.name,{...b,index:i}]));
if(!String(d.skeleton?.spine).startsWith('4.1.'))throw Error('This full rig analyzer targets the verified 4.1 export schema. Inspect 4.3 separately; do not relabel versions.');
function branch(name){const b=byName.get(name);if(!b)return 'unknown:'+name;if(!b.parent)return '(root)';return byName.get(b.parent)?.parent?branch(b.parent):b.name;}
const groups=new Map();for(const b of d.bones){const k=branch(b.name);if(!groups.has(k))groups.set(k,{name:k,setup:byName.get(k)||{},bones:[],slots:[],attachments:[],constraints:[],writers:[]});groups.get(k).bones.push(b.name);}
const slots=new Map(d.slots.map((s,i)=>[s.name,{...s,index:i}]));for(const s of d.slots)groups.get(branch(s.bone)).slots.push(s.name);
const clips=[];for(const skin of d.skins)for(const [slotName,entries]of Object.entries(skin.attachments||{}))for(const [key,a]of Object.entries(entries)){
 const slot=slots.get(slotName),item={skin:skin.name,slot:slotName,slotIndex:slot?.index,bone:slot?.bone,name:key,type:a.type||'region',path:a.path||key,weightedBones:[]};
 const rawLength=a.uvs?.length??(a.vertexCount*2);if(a.vertices&&Number.isFinite(rawLength)&&a.vertices.length!==rawLength){let n=0,used=new Set();while(n<a.vertices.length){let count=a.vertices[n++];if(!Number.isInteger(count)||count<0||count>d.bones.length)throw Error('Invalid weighted vertex '+key);while(count--){used.add(d.bones[a.vertices[n]]?.name);n+=4;}}item.weightedBones=[...used];item.weightedBranches=[...new Set(item.weightedBones.map(branch))];}
 groups.get(branch(slot.bone)).attachments.push(item);
 if(a.type==='clipping')clips.push({...item,setupAttachment:slot.attachment||null,end:a.end||null,endIndex:slots.get(a.end)?.index,vertices:a.vertices,vertexCount:a.vertexCount,rangeInterpretation:a.end===slotName?"official-renderer-continues-after-clipStart; ends-at-skeleton-end":"ends-after-end-slot",drawRange:d.slots.slice(slot.index+1,a.end&&slots.get(a.end).index>slot.index?slots.get(a.end).index+1:undefined).map(s=>({slot:s.name,branch:branch(s.bone)}))});
}
const constraints=[];for(const kind of ['ik','transform','path'])for(const c of d[kind]||[]){const x={kind,...c,targetBranch:kind==='path'?branch(slots.get(c.target)?.bone):branch(c.target),boneBranches:(c.bones||[]).map(branch)};constraints.push(x);for(const k of new Set([x.targetBranch,...x.boneBranches]))groups.get(k)?.constraints.push(c.name);}
const directions={},actions={};function leaves(o,p=[],out=[]){if(o&&typeof o==='object'){if(Array.isArray(o)&&(!o.length||o[0]&&typeof o[0]==='object'&&'time'in o[0]||o[0]&&typeof o[0]==='object'&&p.length>=3)){out.push({path:p.join('/'),keys:o});}else for(const[k,v]of Object.entries(o))leaves(v,[...p,k],out);}return out;}
for(const[name,a]of Object.entries(d.animations)){let used=new Set();for(const b of Object.keys(a.bones||{}))used.add(branch(b));for(const s of Object.keys(a.slots||{}))used.add(branch(slots.get(s)?.bone));for(const kind of ['ik','transform'])for(const c of Object.keys(a[kind]||{})){let x=constraints.find(x=>x.kind===kind&&x.name===c);if(x){used.add(x.targetBranch);x.boneBranches.forEach(b=>used.add(b));}}let record={categories:Object.keys(a),directBoneBranches:[...new Set(Object.keys(a.bones||{}).map(branch))],referencedBranches:[...used],boneCount:Object.keys(a.bones||{}).length,slotCount:Object.keys(a.slots||{}).length,events:a.events||[]};actions[name]=record;for(const k of used)groups.get(k)?.writers.push(name);if(name.startsWith('方向_'))directions[name]={...record,rawTimelines:a,propertyPaths:leaves(a)};}
// Deform and draw-order also write branch presentation; include these instead of inferring
// that all animation effects are represented by direct bone/slot timelines.
for(const [name,a] of Object.entries(d.animations)){
 const record=actions[name],deformedSlots=new Set();
 for(const skin of Object.values(a.attachments||a.deform||{}))for(const slot of Object.keys(skin))deformedSlots.add(slot);
 record.deformedSlots=[...deformedSlots];
 record.drawOrderSlots=[...new Set((a.drawOrder||[]).flatMap(frame=>(frame.offsets||[]).map(offset=>offset.slot)))];
 const extra=[...record.deformedSlots,...record.drawOrderSlots].map(slot=>branch(slots.get(slot)?.bone));
 record.referencedBranches=[...new Set([...record.referencedBranches,...extra])];
 for(const key of extra){const group=groups.get(key);if(group&&!group.writers.includes(name))group.writers.push(name);}
}
for(const group of groups.values())group.attachmentCounts=group.attachments.reduce((counts,a)=>{counts[a.type]=(counts[a.type]||0)+1;return counts;},{});
const result={source:path.resolve(input),sha256:crypto.createHash('sha256').update(fs.readFileSync(input)).digest('hex'),skeleton:d.skeleton,counts:{bones:d.bones.length,slots:d.slots.length,skins:d.skins.length,animations:Object.keys(d.animations).length,ik:(d.ik||[]).length,transform:(d.transform||[]).length},branches:[...groups.values()],constraints,clipping:clips,directions,actions};fs.writeFileSync(output,JSON.stringify(result,null,2));console.log(JSON.stringify({counts:result.counts,branches:result.branches.map(g=>({name:g.name,bones:g.bones.length,slots:g.slots.length,attachments:g.attachments.length,setup:{x:g.setup.x,y:g.setup.y},writers:g.writers.length})),directions:Object.fromEntries(Object.entries(directions).map(([k,v])=>[k,v.rawTimelines])),clipping:clips.map(c=>({slot:c.slot,setup:c.setupAttachment,end:c.end,vertices:c.vertices,range:c.drawRange.length}))},null,2));
