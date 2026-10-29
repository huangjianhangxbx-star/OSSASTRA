/* 图鉴纯逻辑：检索、分页和ID选择集；不持有媒体文件、任意shell或Unity作者状态。 */
(function(root,factory){const api=factory();if(typeof module==='object'&&module.exports)module.exports=api;else root.GroundCatalogCore=api;})(typeof globalThis!=='undefined'?globalThis:this,function(){
 'use strict';
 function filterRows(rows,options={}){
  const query=(options.query||'').trim().toLocaleLowerCase();
  const favorites=new Set(options.favorites||[]),selected=new Set(options.selected||[]);
  return rows.filter(row=>{
   const values=[row.assetId,row.name,row.displayName,row.source?.container,row.sequence?.group].filter(Boolean).join(' ').toLocaleLowerCase();
   return (!query||values.includes(query))&&(!options.kind||row.kind===options.kind)&&
    (!options.status||row.compatibility===options.status)&&(!options.evidence||row.evidenceLevel===options.evidence)&&
    (!options.source||row.source?.container===options.source)&&(!options.favoritesOnly||favorites.has(row.assetId))&&
    (!options.selectedOnly||selected.has(row.assetId))&&(!options.representativesOnly||!row.sequence||row.sequence.firstFrame===true);
  });
 }
 function boundPage(page,count,size){return Math.max(1,Math.min(Math.ceil(count/size)||1,Math.trunc(Number(page))||1));}
 function exportSelection(catalog,ids){
  const index=new Map(catalog.entries.map(row=>[row.assetId,row]));
  const entries=[...new Set(ids)].map(id=>{if(!index.has(id))throw Error('unknown assetId: '+id);return {assetId:id,revision:index.get(id).revision};});
  return {format:'ground-workshop-selection',schemaVersion:1,catalogId:catalog.catalogId,version:catalog.version,entries};
 }
 return {filterRows,boundPage,exportSelection};
});
