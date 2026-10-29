// 自有合成行验证真实浏览逻辑：过滤不串状态、跳页有界、选择集只含ID与修订。
const assert=require('node:assert/strict');
const path=require('node:path');
const fs=require('node:fs');
const modulePath=path.join(__dirname,'catalog-ui-core.js');
if(!fs.existsSync(modulePath)) {
  assert.fail('catalog UI core not implemented: filtering and selection export unavailable');
}
const api=require(modulePath);
const rows=[
 {assetId:'a',revision:'r1',name:'Moss',displayName:'草地',kind:'GroundDetail',compatibility:'Ready',evidenceLevel:'SOURCE_VERIFIED',source:{container:'x.bundle'}},
 {assetId:'b',revision:'r2',name:'Flower',displayName:'花',kind:'UprightSprite',compatibility:'StaticPreviewOnly',evidenceLevel:'INFERRED',source:{container:'y.bundle'}},
 {assetId:'c',revision:'r3',name:'Stone',displayName:'石',kind:'FlatSprite',compatibility:'Ready',evidenceLevel:'UNITY_ADAPTATION',source:{container:'x.bundle'}}
];
assert.deepEqual(api.filterRows(rows,{query:'草'}).map(x=>x.assetId),['a']);
assert.deepEqual(api.filterRows(rows,{query:'x.bundle',kind:'FlatSprite',status:'Ready'}).map(x=>x.assetId),['c']);
assert.deepEqual(api.filterRows(rows,{favoritesOnly:true,favorites:['b']}).map(x=>x.assetId),['b']);
assert.deepEqual(api.filterRows(rows,{selectedOnly:true,selected:['a','c'],evidence:'UNITY_ADAPTATION'}).map(x=>x.assetId),['c']);
assert.equal(api.boundPage(999,61,60),2);
assert.equal(api.boundPage(-2,0,60),1);
assert.equal(api.boundPage('bad',61,60),1);
const payload=api.exportSelection({catalogId:'own',version:'v1',entries:rows},['c','a']);
assert.deepEqual(payload,{format:'ground-workshop-selection',schemaVersion:1,catalogId:'own',version:'v1',entries:[{assetId:'c',revision:'r3'},{assetId:'a',revision:'r1'}]});
assert.throws(()=>api.exportSelection({catalogId:'own',version:'v1',entries:rows},['z']),/unknown/);
assert.deepEqual(api.filterRows([
 {...rows[0],sequence:{group:'own-idle',firstFrame:true}},
 {...rows[1],sequence:{group:'own-idle',firstFrame:false}},rows[2]
],{representativesOnly:true}).map(x=>x.assetId),['a','c']);
console.log('9 catalog UI behavior checks passed');
