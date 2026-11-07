import { createHash } from 'node:crypto';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve, join } from 'node:path';
import { pathToFileURL, fileURLToPath } from 'node:url';

const nativeRoot = 'E:/WORLDCREATOR/XingHaiHuiLang/Origin/自制工具/map_editor';
const native = async name => import(pathToFileURL(join(nativeRoot, 'core', name)).href);
const grid = 0.25;
const hills = [
  { id:'hill-a', centerM:[-24,-4], radiusM:3, heightM:0.75 },
  { id:'hill-b', centerM:[24,12], radiusM:4, heightM:1.25 },
];
const pond = { centerM:[15,-16], radiusM:[3,2], bedLevelM:-0.5, waterLevelM:-0.18 };

// 仅生成原生编辑数据；可见面合并完全交给地编原有 meshMap。
export async function generateTerrain() {
  const [{ createAsset, validateAsset }, { toEditingMap }, { meshMap }, { paletteFromMap }] = await Promise.all([
    native('workshop-documents.ts'), native('workshop-adapter.ts'), native('mesher.ts'), native('palette.ts'),
  ]);
  const asset = createAsset('voxel-map-01-terrain');
  asset.revision = 1;
  asset.name = 'VOXEL-MAP-01 原生圆角地表、两处小坡与凹形水洼';
  asset.palette = ['#ffffff','#725239','#756048','#67747c','#f2f6e8','#a18261','#4b5c64','#d3c29f'];
  asset.sideColor = 1;
  asset.materialProfile = paletteFromMap({ ...asset, mapId:asset.assetId });
  asset.cells = [];
  const waterTilesM = [];
  let columns = 0, basinColumns = 0, minTop = Infinity, maxTop = -Infinity;
  const hillColumns = new Map(hills.map(h => [h.id,0]));
  for (let y=-104; y<104; y++) {
    for (let x=-128; x<128; x++) {
      const px=(x+0.5)*grid, pz=(y+0.5)*grid;
      if ((Math.abs(px)/32)**4+(Math.abs(pz)/26)**4>1) continue;
      let top = 0;
      for (const hill of hills) {
        const radial=Math.hypot(px-hill.centerM[0],pz-hill.centerM[1])/hill.radiusM;
        if (radial>=1) continue;
        const levels=Math.round(hill.heightM/grid);
        const plateau=0.28;
        const level=radial<=plateau ? levels : Math.ceil((1-radial)/(1-plateau)*levels);
        top=Math.max(top,level);
        if (level>0) hillColumns.set(hill.id,hillColumns.get(hill.id)+1);
      }
      const pr=Math.hypot((px-pond.centerM[0])/pond.radiusM[0],(pz-pond.centerM[1])/pond.radiusM[1]);
      if (pr<1) {
        top=pr<0.66 ? -2 : -1;
        basinColumns++;
        waterTilesM.push([x*grid,y*grid]);
      }
      const color=top<0 ? 2 : 0;
      for (let z=-3; z<top; z++) asset.cells.push({x,y,z,color,owner:'height'});
      columns++;
      minTop=Math.min(minTop,top*grid); maxTop=Math.max(maxTop,top*grid);
    }
  }
  const valid=validateAsset(asset);
  const authorJson=JSON.stringify(valid);
  const sourceSha256=createHash('sha256').update(authorJson,'utf8').digest('hex');
  const editing=toEditingMap(valid);
  const quads=meshMap(editing);
  const surface={
    schema:'ossastra-voxel-surface-1',sourceSha256,assetId:valid.assetId,revision:valid.revision,
    voxelSize:grid,axes:'RH_Z_UP',materialRoles:['grass','soil','basin','soil','grass','soil','basin','soil'],quads,
  };
  const statistics={
    cells:valid.cells.length, columns, footprintSquareMeters:columns*grid*grid,
    quads:quads.length, vertices:quads.length*4, triangles:quads.length*2,
    nativeChunks:new Set(quads.map(q=>q.chunk)).size,
    exposedUnitFaces:quads.reduce((n,q)=>n+q.w*q.h,0), minTopM:minTop,maxTopM:maxTop,
    basinColumns, basinSquareMeters:basinColumns*grid*grid,
    hillColumns:Object.fromEntries(hillColumns),
  };
  const layout={
    schema:'ossastra-voxel-terrain-layout-1',assetId:valid.assetId,sourceSha256,
    axes:'UNITY_Y_UP',voxelSize:grid,bottomM:-0.75,baseTopM:0,
    boundsXZ:{min:[-32,-26],max:[32,26],shape:'superellipse',power:4},
    hills:hills.map(h=>({...h,centerUnityM:[h.centerM[0],0,h.centerM[1]],stepHeightM:grid,plateauRadiusM:h.radiusM*0.28})),
    pond:{...pond,centerUnityM:[pond.centerM[0],pond.waterLevelM,pond.centerM[1]],waterTileSizeM:grid,waterTilesM},
    preservedFlatAreas:[{name:'original-building',centerM:[-15,-10],topM:0},{name:'business-row',fromM:[7,-24],toM:[21,-24],topM:0}],
    statistics,
  };
  if (statistics.cells>250000 || statistics.quads>250000 || statistics.vertices>1000000) throw new Error('Native terrain exceeds agreed budgets');
  return {asset:valid,authorJson,surface,layout};
}

async function main() {
  const output=resolve(process.argv[2] ?? 'E:/WORLDCREATOR/XingHaiHuiLang/研究/VOXEL-MAP-01/Terrain');
  const result=await generateTerrain();
  await mkdir(output,{recursive:true});
  await writeFile(join(output,'voxel-map-01-terrain.xhasset.json'),result.authorJson,'utf8');
  await writeFile(join(output,'voxel-map-01-terrain.surface.json'),JSON.stringify(result.surface),'utf8');
  await writeFile(join(output,'terrain-layout.json'),JSON.stringify(result.layout,null,2),'utf8');
  await writeFile(join(output,'terrain-generation-report.json'),JSON.stringify({
    nativeRoot,producer:'createAsset/validateAsset -> toEditingMap -> meshMap',
    sourceSha256:result.surface.sourceSha256,statistics:result.layout.statistics,
    rules:['author remains editable Workshop asset','surface frozen from native mesher','3 slots grass/soil/basin','8 palette entries preserved','base top 0 meters; real basin below 0','no per-frame runtime manager'],
  },null,2),'utf8');
  process.stdout.write(JSON.stringify({output,sourceSha256:result.surface.sourceSha256,...result.layout.statistics},null,2)+'\n');
}

if (process.argv[1] && resolve(process.argv[1]) === resolve(fileURLToPath(import.meta.url))) await main();
