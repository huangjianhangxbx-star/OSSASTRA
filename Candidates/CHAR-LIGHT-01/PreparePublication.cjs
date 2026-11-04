// 只准备定向索引；不提交、不推送。原staged非空、HEAD变化或索引被他人写入时拒绝安装。
const fs=require('fs'),path=require('path'),cp=require('child_process'),crypto=require('crypto');
const root='E:/WORLDCREATOR/new/Project_ANKALUTE',archive='E:/WORLDCREATOR/XingHaiHuiLang/研究/角色光感/CHAR-LIGHT-01';
if(process.cwd().replaceAll('\\','/')!==root)throw Error('Wrong project');
const git=(args,env={})=>cp.execFileSync('git',args,{cwd:root,encoding:'utf8',env:{...process.env,...env}}).trim();
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const head=git(['rev-parse','HEAD']);
if(head!=='da2f4af8a9d6837449ea66a2242aa35124bc9b7b'||git(['branch','--show-current'])!=='main')throw Error('Unexpected branch/HEAD');
if(git(['remote','get-url','origin'])!=='https://github.com/huangjianhangxbx-star/OSSASTRA.git')throw Error('Unexpected remote');
if(git(['diff','--cached','--name-only']))throw Error('Existing staged work must not be replaced');
const base=JSON.parse(fs.readFileSync(path.join(archive,'source-baseline.json'),'utf8').replace(/^\uFEFF/,''));
const existingDirty=new Set(base.status.map(s=>s.slice(3)));
const files=new Set();
function add(p){p=p.replaceAll('\\','/');if(existingDirty.has(p))throw Error('Original dirty cannot be staged: '+p);if(!fs.statSync(p).isFile()||!fs.realpathSync(p).replaceAll('\\','/').startsWith(root+'/'))throw Error('External/missing file: '+p);if(/\.(unity|prefab|asset|mat|png|wav|mp4|zip|dll)$/i.test(p)||p.startsWith('Assets/Script/')||p.startsWith('Assets/Scripts/'))throw Error('Forbidden publication file: '+p);files.add(p);}
function walk(dir,test){for(const entry of fs.readdirSync(dir,{withFileTypes:true})){const p=dir+'/'+entry.name;if(entry.isDirectory()){if(!['bin','obj'].includes(entry.name))walk(p,test);}else if(test(p))add(p);}}
for(const d of ['Assets/OSSASTRA/Scripts/CharacterLighting','Assets/OSSASTRA/Scripts/Development/CharacterLighting']){walk(d,p=>/\.(cs|hlsl|shader|meta)$/.test(p));add(d+'.meta');}
for(const p of ['Characters/Runtime/CharacterRoot.cs','Development/Environment/EnvironmentSimulationPanel.cs','EnvironmentSimulation/Rendering/EnvironmentLighting.hlsl','EnvironmentSimulation/Rendering/EnvironmentSkyBinding.cs','EnvironmentSimulation/Runtime/EnvironmentRoot.cs','SceneVisibility/Rooms/Rendering/RoomPositivePass.cs','SceneVisibility/Rooms/Rendering/RoomSightDepthPlane.shader','SceneVisibility/Rooms/Rendering/RoomSightSpine.shader'])add('Assets/OSSASTRA/Scripts/'+p);
walk('Candidates/CHAR-LIGHT-01',p=>/\.cs\.txt$/.test(p)||/\.(md|cjs)$/.test(p));
walk('docs/ossastra/CHAR-LIGHT-01',p=>p.endsWith('.md'));
add('docs/agent-sync/tasks/2026-11-04-char-light-01.md');add('OSSASTRA_RULES.md');
const index=path.resolve(git(['rev-parse','--git-path','index'])),before=fs.readFileSync(index),indexSha=hash(before),temporary=path.join(archive,'publication.index');
const env={GIT_INDEX_FILE:temporary};git(['read-tree','HEAD'],env);git(['add','-f','--',...Array.from(files).sort()],env);
const changed=git(['diff','--cached','--name-only','-z'],env).split('\0').filter(Boolean);
if(changed.some(p=>!files.has(p)))throw Error('Index contains a non-whitelisted path');
// Unity生成的.meta空标量保留原样；只查源码/文档，不为EOF空行重新导入项目。
git(['-c','core.whitespace=blank-at-eol,space-before-tab,cr-at-eol,-blank-at-eof','diff','--cached','--check','--',...changed.filter(p=>!p.endsWith('.meta'))],env);
const manifest={head,branch:'main',remote:'origin',sourceIndexHash:indexSha,paths:changed,excluded:'All original dirty paths, shared sync prefixes, scene/config/material/Spine/media/saves; only this task source/contracts/docs are included.'};
fs.writeFileSync(path.join(archive,'publication-manifest.json'),JSON.stringify(manifest,null,2));
if(process.argv.includes('--stage')){if(git(['rev-parse','HEAD'])!==head||hash(fs.readFileSync(index))!==indexSha)throw Error('Concurrent index or HEAD change');fs.writeFileSync(path.join(archive,'git-index-before.bin'),before);fs.copyFileSync(temporary,index);}
console.log(JSON.stringify({prepared:true,staged:process.argv.includes('--stage'),paths:changed.length,manifest:path.join(archive,'publication-manifest.json')}));
