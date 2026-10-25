// 原创工程诊断短音：确定性数学合成，无外部采样、无原作音频。
// Run: node Candidates/AUDIO-M02/Tests/GenerateDiagnosticSfx.mjs
import fs from 'node:fs';
const out='Assets/OSSASTRA/Audio/SFX/Diagnostics';fs.mkdirSync(out,{recursive:true});
const definitions=[['release',.16,640],['hit',.2,125],['hurt',.27,185],['guard',.2,920],['dodge',.2,410],['land',.25,90],['heal',.36,660],['ui',.1,880]];
for(let index=0;index<definitions.length;index++){
 const [name,duration,frequency]=definitions[index],rate=24000,count=Math.round(duration*rate),b=Buffer.alloc(44+count*2);let seed=1729+index;
 b.write('RIFF',0);b.writeUInt32LE(b.length-8,4);b.write('WAVEfmt ',8);b.writeUInt32LE(16,16);b.writeUInt16LE(1,20);b.writeUInt16LE(1,22);b.writeUInt32LE(rate,24);b.writeUInt32LE(rate*2,28);b.writeUInt16LE(2,32);b.writeUInt16LE(16,34);b.write('data',36);b.writeUInt32LE(count*2,40);
 for(let i=0;i<count;i++){
  const t=i/rate,u=i/(count-1);seed=(Math.imul(seed,1664525)+1013904223)>>>0;const noise=seed/2147483648-1;
  const window=Math.sin(Math.PI*u)**2*Math.exp(-u*3),chirp=Math.sin(2*Math.PI*(frequency*t+(name==='release'?1200:50)*t*t));
  const harmonic=Math.sin(2*Math.PI*frequency*1.5*t),mix=name==='dodge'?.82: name==='hit'||name==='land'?.45:.15;
  const sample=.65*window*((1-mix)*(.7*chirp+.3*harmonic)+mix*noise);b.writeInt16LE(Math.max(-32767,Math.min(32767,Math.round(sample*32767))),44+i*2);
 }
 fs.writeFileSync(`${out}/${name}.wav`,b);
}
