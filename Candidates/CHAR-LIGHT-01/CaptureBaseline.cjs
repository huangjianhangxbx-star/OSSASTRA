// 功能：只记录本轮原有dirty、精确受影响源/材质/原档指纹，不修改Unity资产。
const fs=require('fs'),path=require('path'),crypto=require('crypto'),cp=require('child_process');
const archive='E:/WORLDCREATOR/XingHaiHuiLang/研究/角色光感/CHAR-LIGHT-01',out=path.join(archive,'source-baseline.json');
if(fs.existsSync(out))throw Error('baseline exists');
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const status=cp.execFileSync('git',['status','--porcelain=v1','-z'],{encoding:'utf8'}).split('\0').filter(Boolean);
const selected=['Characters/Runtime/CharacterRoot.cs','EnvironmentSimulation/Rendering/EnvironmentSkyBinding.cs','EnvironmentSimulation/Rendering/EnvironmentLighting.hlsl','SceneVisibility/Rooms/Rendering/RoomPositivePass.cs','SceneVisibility/Rooms/Rendering/RoomPositivePropertyScope.cs','SceneVisibility/Rooms/Rendering/RoomSightSpine.shader','SceneVisibility/Rooms/Rendering/RoomSightDepthPlane.shader'].map(x=>'Assets/OSSASTRA/Scripts/'+x);
selected.push('Assets/OSSASTRA/MapResearch/Materials/ActorDepth_0.mat','Assets/OSSASTRA/Prefabs/Research/DSMap02/Materials/ActorDepth_1.mat','Assets/OSSASTRA/GroundWorkshop/Samples/KT-VIS-04/SC_RoomVisibilityLab.unity','docs/agent-sync/PROJECT_STATE.md','docs/agent-sync/TASK_REGISTRY.md');
const files={};for(const p of new Set([...selected,...status.map(s=>s.slice(3)).filter(p=>!p.startsWith('docs/agent-sync/'))]))if(fs.existsSync(p)&&fs.statSync(p).isFile())files[p]={sha:sha(p),bytes:fs.statSync(p).size};
const saveDir='E:/WORLDCREATOR/XingHaiHuiLang/研究/可见性修复/KT-VIS-06/profiles',saves={};
if(fs.existsSync(saveDir))for(const f of fs.readdirSync(saveDir)){const p=path.join(saveDir,f);if(fs.statSync(p).isFile())saves[f]={sha:sha(p),bytes:fs.statSync(p).size};}
fs.writeFileSync(out,JSON.stringify({head:cp.execFileSync('git',['rev-parse','HEAD'],{encoding:'utf8'}).trim(),status,files,saveDir,saves},null,2));console.log(JSON.stringify({dirty:status.length,files:Object.keys(files).length,saves:Object.keys(saves).length}));
