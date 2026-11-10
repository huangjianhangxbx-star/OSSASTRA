'use strict';
const fs=require('fs'),path=require('path'),crypto=require('crypto'),{summarize}=require('./SummaryCore.cjs');
if(process.argv.length!==4)throw Error('Usage: node SummarizeToonCost.cjs input.json output-directory');
const source=path.resolve(process.argv[2]),out=path.resolve(process.argv[3]);if(source===path.join(out,'summary.json'))throw Error('Cannot overwrite raw input');
const bytes=fs.readFileSync(source),summary=summarize(JSON.parse(bytes));summary.source=source;summary.sourceSha256=crypto.createHash('sha256').update(bytes).digest('hex');summary.generatedUtc=new Date().toISOString();
fs.mkdirSync(out,{recursive:true});fs.writeFileSync(path.join(out,'summary.json'),JSON.stringify(summary,null,2));
const f=v=>v===null||v===undefined?'Unknown':v.toFixed(3),pair=m=>f(m.p50)+' / '+f(m.p95);
const lines=['# ToonSurface01 同灯材质成本','','Formal conditions: **'+summary.formalConditions+'**. Stable60: **'+summary.stable60.status+'**.','','| Lighting / backend | Frame p50/p95 ms | GPU p50/p95 ms | GPU delta p50/p95 ms | GPU .5ms budget | CPU main/render inclusive p50 ms |','|---|---:|---:|---:|---|---:|'];
for(const g of summary.lightingGroups)for(const key of ['originalBefore','realToon','toonSurface01','originalAfter']){const r=g[key];lines.push('| '+g.lighting+' / '+key+(r.available?'':' (unavailable)')+' | '+pair(r.frame)+' | '+pair(r.gpu)+' | '+pair(r.gpuIncrement)+' | '+r.gpuIncrement.status+' | '+f(r.cpuMainInclusive.p50)+' / '+f(r.cpuRenderInclusive.p50)+' |');}
lines.push('',summary.interpretation,'','CPU effective increment: **Unknown**, unless separately proven by a complete exclusive decomposition. Report names and source material references are not GPU instruction captures.');
fs.writeFileSync(path.join(out,'summary.md'),lines.join('\n'));console.log(JSON.stringify({source,output:out,formalConditions:summary.formalConditions,budgets:summary.lightingGroups.map(g=>({lighting:g.lighting,status:g.toonSurface01.gpuIncrement.status}))}));
