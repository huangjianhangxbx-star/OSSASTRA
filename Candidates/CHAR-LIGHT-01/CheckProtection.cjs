// 核对任务接手时的原有dirty字节和原业务档；不修改任何受测文件。
const fs=require('fs'),crypto=require('crypto'),path=require('path');
const root='E:/WORLDCREATOR/new/Project_ANKALUTE';
const dir='E:/WORLDCREATOR/XingHaiHuiLang/研究/角色光感/CHAR-LIGHT-01';
if(process.cwd().replaceAll('\\','/')!==root)throw Error('Wrong project');
const b=JSON.parse(fs.readFileSync(path.join(dir,'source-baseline.json'),'utf8').replace(/^\uFEFF/,''));
const owned=new Set(['Characters/Runtime/CharacterRoot.cs','Development/Environment/EnvironmentSimulationPanel.cs','EnvironmentSimulation/Rendering/EnvironmentLighting.hlsl','EnvironmentSimulation/Rendering/EnvironmentSkyBinding.cs','EnvironmentSimulation/Runtime/EnvironmentRoot.cs','SceneVisibility/Rooms/Rendering/RoomPositivePass.cs','SceneVisibility/Rooms/Rendering/RoomSightSpine.shader','SceneVisibility/Rooms/Rendering/RoomSightDepthPlane.shader'].map(x=>'Assets/OSSASTRA/Scripts/'+x));
owned.add('Assets/OSSASTRA/GroundWorkshop/Samples/KT-VIS-04/SC_RoomVisibilityLab.unity'); // Explicitly authorized main integration/defaults; external MainBefore preserves starting dirty.
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const files=Object.entries(b.files).filter(([p])=>!owned.has(p)).map(([p,v])=>({path:p,pass:fs.existsSync(p)&&sha(p)===v.sha}));
const saves=Object.entries(b.saves).map(([p,v])=>({path:p,pass:fs.existsSync(path.join(b.saveDir,p))&&sha(path.join(b.saveDir,p))===v.sha}));
const deletions=b.status.filter(s=>s.startsWith(' D ')).map(s=>({path:s.slice(3),pass:!fs.existsSync(s.slice(3))}));
const result={pass:[...files,...saves,...deletions].every(v=>v.pass),files,saves,deletions,scope:'Original captured dirty files, unchanged seam and original business profiles; task-owned source changes excluded, no claim of a new full-project media freeze.'};
fs.writeFileSync(path.join(dir,'final-protection.json'),JSON.stringify(result,null,2));
console.log(JSON.stringify({pass:result.pass,files:files.length,saves:saves.length,originalDeletions:deletions.length,failed:[...files,...saves,...deletions].filter(v=>!v.pass)}));
