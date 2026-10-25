/* 自有诊断PCM与本地依赖准备。仅写任务目录；不改银行、不覆盖已有媒体。 */
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const project = 'E:/WORLDCREATOR/new/Project_ANKALUTE';
const diagnostic = path.join(project, 'Assets/OSSASTRA/Audio/BGM/Diagnostics');
const local = path.join(project, 'Assets/OSSASTRA/LocalMedia/AUDIO-M01');
fs.mkdirSync(diagnostic, { recursive: true });
fs.mkdirSync(local, { recursive: true });
const rate = 16000, count = 153600;
for (const [id, hz] of [['title', 110], ['home', 146.83], ['chapter1', 98], ['boss', 82.41]]) {
  for (let layer = 0; layer < 3; layer++) {
    const b = Buffer.alloc(44 + count * 2);
    b.write('RIFF'); b.writeUInt32LE(36 + count * 2, 4); b.write('WAVEfmt ', 8);
    b.writeUInt32LE(16, 16); b.writeUInt16LE(1, 20); b.writeUInt16LE(1, 22);
    b.writeUInt32LE(rate, 24); b.writeUInt32LE(rate * 2, 28); b.writeUInt16LE(2, 32); b.writeUInt16LE(16, 34);
    b.write('data', 36); b.writeUInt32LE(count * 2, 40);
    for (let i = 0; i < count; i++) {
      const time = i / rate, beat = time * 100 / 60, phase = beat % 1;
      let v;
      if (layer === 0) v = Math.sin(2 * Math.PI * hz * time) * .17 + Math.sin(2 * Math.PI * hz * 1.5 * time) * .055;
      else if (layer === 1) v = Math.sin(2 * Math.PI * (hz * .5 - 20 * phase) * time) * Math.exp(-phase * 18) * .28;
      else v = Math.sin(2 * Math.PI * [hz * 3, hz * 3.5, hz * 4, hz * 3.5][Math.floor(beat) % 4] * time) * Math.exp(-phase * 7) * .13;
      b.writeInt16LE(Math.trunc(Math.max(-1, Math.min(1, v)) * 32767), 44 + i * 2);
    }
    const output = path.join(diagnostic, `${id}_${layer}.wav`);
    if (fs.existsSync(output)) {
      if (!fs.readFileSync(output).equals(b)) throw new Error(`Refuse overwrite ${output}`);
    } else fs.writeFileSync(output, b, { flag: 'wx' });
  }
}
const records = [];
for (const id of ['title', 'home', 'chapter1']) {
  const source = `E:/WORLDCREATOR/XingHaiHuiLang/Origin/work/SCENE-2D-01/checkout/work/AUDIO-01/music/${id}.wav`;
  if (!fs.existsSync(source)) { records.push({ id, status: 'LOCAL_MEDIA_UNAVAILABLE' }); continue; }
  const target = path.join(local, `${id}.wav`);
  if (!fs.existsSync(target)) fs.copyFileSync(source, target, fs.constants.COPYFILE_EXCL);
  const hash = p => crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
  if (hash(source) !== hash(target)) throw new Error(`Local dependency differs: ${id}`);
  records.push({ id, bytes: fs.statSync(target).size, sha256: hash(target), distribution: 'LOCAL ONLY' });
}
process.stdout.write(JSON.stringify({ diagnosticClips: 12, rate, seconds: 9.6, bpm: 100, local: records }));
