// Only selected local research media. Source files are immutable; output PCM and clip-referencing SO remain Git-excluded.
import fs from 'node:fs';import path from 'node:path';import crypto from 'node:crypto';
const work='E:/WORLDCREATOR/XingHaiHuiLang/work/OSSASTRA/AUDIO-BB-FUSION-20261027';
const audit=JSON.parse(fs.readFileSync(work+'/content-audit.json','utf8').replace(/^\uFEFF/,''));
const sources=new Map([...audit.catalog,...audit.additionalSelectedSidecars,...audit.authorizedTargetedDecode.entries].map(x=>[x.id,x]));
const output=work+'/Prepared';fs.mkdirSync(output,{recursive:true});
const events=[];const media=new Map();const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
function wave(b){let format,channels,rate,bits,start,length;for(let p=12;p+8<=b.length;){const id=b.toString('ascii',p,p+4),n=b.readUInt32LE(p+4);if(id==='fmt '){format=b.readUInt16LE(p+8);channels=b.readUInt16LE(p+10);rate=b.readUInt32LE(p+12);bits=b.readUInt16LE(p+22);}if(id==='data'){start=p+8;length=n;break;}p+=8+n+(n%2);}if(format!==1||bits!==16||!start||start+length>b.length)throw Error('Expected PCM16 source');return{channels,rate,start,length};}
function prepare(id){if(media.has(id))return id;const src=sources.get(id);if(!src||src.eligibleAsBloodborne===false)throw Error('Missing or non-BB source '+id);const bytes=fs.readFileSync(src.sourcePath);if(sha(bytes)!==src.sourceSha256.toLowerCase())throw Error('Source hash '+id);const w=wave(bytes);let peak=0,sum=0,full=0;for(let i=w.start;i<w.start+w.length;i+=2){const v=bytes.readInt16LE(i)/32768;peak=Math.max(peak,Math.abs(v));sum+=v*v;if(Math.abs(v)>=.999)full++;}const rms=Math.sqrt(sum/(w.length/2));const gain=Math.min(1,.78/Math.max(peak,.00001),.12/Math.max(rms,.00001));const result=Buffer.from(bytes);for(let i=w.start;i<w.start+w.length;i+=2)result.writeInt16LE(Math.round(bytes.readInt16LE(i)*gain),i);const dest=output+'/'+id.replace(/[^a-z0-9._-]/gi,'_')+'.wav';if(fs.existsSync(dest)&&sha(fs.readFileSync(dest))!==sha(result))throw Error('Conflicting prepared output '+dest);fs.writeFileSync(dest,result);media.set(id,{id,sourcePath:src.sourcePath,sourceSha256:sha(bytes),outputPath:dest,outputSha256:sha(result),assetPath:'Assets/OSSASTRA/Audio/BloodborneLocal/Media/'+path.basename(dest),durationSeconds:src.durationSeconds,sourcePeak:peak,sourceRms:rms,sourceFullScale:full,gain,peak:peak*gain,rms:rms*gain,fsbPath:src.fsbPath,fsbSha256:src.fsbSha256,stream:src.stream,streamName:src.streamName,confidence:src.confidence||src.identity,identity:src.identity||'STATIC_NAMED_CANDIDATE',origin:src.origin||'GAME_DERIVED',humanAudition:'PENDING_HUMAN'});return id;}
function add(id,clips,options={}){clips.forEach(prepare);events.push({id,clips,volume:.35,priority:55,interval:0,loop:false,...options});}
const group=a=>a.map(n=>'sprj_main_eng.'+n);
const base={
 'blade.swing.1':group([271]),'blade.swing.2':group([272]),'blade.swing.3':group([273]),'blade.swing.4':group([274]),
 'blade.fast':group([271,272]),'blade.heavy':group([273,274]),'blade.impact':group([410,411]),'blade.impact.heavy':group([427,428]),
 'guard.metal':group([415,407,408,409]),'gun.fire':group([157,158]),'gun.reload':group([822,823]),'launcher.fire':group([158]),'blast.small':group([821,152,153]),
 'impact.flesh':group([410,411]),'impact.metal':group([407,408,409,415]),'impact.stone':group([412,413,417]),'impact.wood':group([414,418,442]),
 'movement.dodge':group([33]),'item.heal':group([1]),'skill.electric':group([506]),'skill.fire':group([498]),'item.food':group([1])
};
for(const role of ['player','companion','enemy']){
 const roleGain=role==='player'?1:role==='companion'?.65:.6;
 for(const [cue,clips] of Object.entries(base))add(role+'.'+cue,clips,{volume:(cue.includes('impact')?.42:cue.includes('blast')?.38:.32)*roleGain,priority:cue.includes('impact')?70:cue.includes('reload')?60:55,interval:cue.includes('impact')?.035:0});
 add(role+'.body.hurt',role==='enemy'?['sprj_c1050.28','sprj_c1050.29','sprj_c1050.30']:group([605,606,607,608,609]),{volume:.22*roleGain,priority:85,interval:.28});
 add(role+'.body.death',role==='enemy'?['sprj_c1050.31','sprj_c1050.32']:group([610]),{volume:.35*roleGain,priority:95});
 if(role==='enemy')add('enemy.attack',['sprj_c1050.26'],{volume:.25,priority:55,interval:1.5});
 for(const surface of ['generic','stone','wood','dirt','metal'])for(const mode of ['walk','run']){
  const clips=surface==='wood'?group([341,342]):surface==='dirt'?group(mode==='walk'?[385,386]:[380,381,382]):group(mode==='walk'?[398,399,400]:[395,396,397]);
  // generic/metal have no proven dedicated set; declared candidates, not fabricated original ground identity.
  add(role+'.footstep.'+surface+'.'+mode,clips,{volume:.16*roleGain,priority:20,interval:.16,notes:surface==='metal'?'P0_CONTENT_GAP: stone-footstep closest candidate; no dedicated metal step':''});
 }
}
add('ui.control.switch',group([3]),{volume:.18,priority:80});
add('ui.inventory.open',group([308]),{volume:.18,priority:80});add('ui.inventory.close',group([307]),{volume:.18,priority:80});
add('interaction.container.open',group([803,804]),{volume:.22,priority:60});add('interaction.container.close',group([805,806]),{volume:.22,priority:60});
add('ambience.torch',group([959]),{volume:.10,priority:10,loop:true});add('ambience.rain',['sprj_m36.64'],{volume:.09,priority:10,loop:true});
add('ambience.fire',['sprj_smain.13'],{volume:.08,priority:10,loop:true});
const music=['music.smain.musicbox','music.pscom.ludwig01.lab_stereo','music.pscom.clock01.lab_stereo'];music.forEach(prepare);
const manifest={schemaVersion:1,task:'AUDIO-BB-FUSION',privateOnly:true,sourceAuditSha256:sha(fs.readFileSync(work+'/content-audit.json')),allSourcesUnchanged:true,media:[...media.values()],events,music};
for(const m of manifest.media)if(sha(fs.readFileSync(m.sourcePath))!==m.sourceSha256)throw Error('Source changed '+m.id);
fs.writeFileSync(work+'/prepared-manifest.json',JSON.stringify(manifest,null,2));
const report='E:/WORLDCREATOR/new/Project_ANKALUTE/Candidates/AUDIO-BB-FUSION/reports';
fs.writeFileSync(report+'/BB_SOURCE_MAP.csv','semantic_id,media_id,original_fsb,substream,source_sha256,derived_sha256,duration_sec,confidence,bus,license_scope,human_listening\n'+events.flatMap(e=>e.clips.map(id=>{const m=media.get(id);return[e.id,id,m.fsbPath,m.stream,m.sourceSha256,m.outputSha256,m.durationSeconds,m.confidence,e.id.split('.')[0],'LOCAL_RESEARCH_ONLY','PENDING_HUMAN'].map(x=>JSON.stringify(String(x??''))).join(',')})).join('\n')+'\n');
fs.writeFileSync(report+'/MEDIA_LEVELS.json',JSON.stringify({sourceClippingRetainedAsEvidence:true,sources:manifest.media.map(m=>({id:m.id,sourcePeak:m.sourcePeak,sourceFullScale:m.sourceFullScale,gain:m.gain,derivedPeak:m.peak,derivedRms:m.rms}))},null,2));
console.log(JSON.stringify({media:media.size,events:events.length,bytes:manifest.media.reduce((n,m)=>n+fs.statSync(m.outputPath).size,0),sourceUnchanged:true}));
