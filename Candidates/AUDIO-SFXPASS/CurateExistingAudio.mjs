// 本地受限音频整理：只裁剪/淡入淡出/调整现有PCM，不重采样、不生成新音色、不改原文件。
// 执行：node CurateExistingAudio.mjs；所有生成物仅存放本脚本同目录的Derived。
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const outputDirectory = path.join(scriptDirectory, 'Derived');
const project = 'E:/WORLDCREATOR/new/Project_ANKALUTE';
const reference = `${project}/Assets/OSSASTRA/Art/AudioReferenceLocal`;
const gunPack = `${project}/Assets/Gun Sounds Pack Vol 1`;
const feelSamples = `${project}/Assets/Feel/NiceVibrations/HapticSamples`;
const clips = [
  { id: 'blade-release', source: `${reference}/release.wav`, family: 'blade-whoosh', targetRms: .06, fadeOutMs: 12, provenance: 'Cinderia原作提取：匕首斩改；仅本机参考', meaning: '现有近战挥击候选，保持正常音高' },
  { id: 'blade-hit', source: `${reference}/hit.wav`, family: 'blade-impact', targetRms: .06, fadeOutMs: 15, provenance: 'Cinderia原作提取：a击中；仅本机参考', meaning: '现有命中候选' },
  { id: 'character-hurt', source: `${reference}/hurt.wav`, family: 'character-hurt', targetRms: .06, fadeOutMs: 20, provenance: 'Cinderia原作提取：人物受击；仅本机参考', meaning: '现有受伤候选，不冒充新角色专属配音' },
  { id: 'gun-5-4', source: `${gunPack}/Gun Shot/Gunshot 5-4.wav`, family: 'gun-5', targetRms: .06, fadeOutMs: 15, provenance: '工程已有Gun Sounds Pack Vol 1；包内未发现许可文档，仅本机', meaning: '现有枪击同家族变体候选' },
  { id: 'gun-5-5', source: `${gunPack}/Gun Shot/Gunshot 5-5.wav`, family: 'gun-5', targetRms: .06, fadeOutMs: 15, provenance: '工程已有Gun Sounds Pack Vol 1；包内未发现许可文档，仅本机', meaning: '现有枪击同家族变体候选' },
  { id: 'gun-5-6', source: `${gunPack}/Gun Shot/Gunshot 5-6.wav`, family: 'gun-5', targetRms: .06, fadeOutMs: 15, provenance: '工程已有Gun Sounds Pack Vol 1；包内未发现许可文档，仅本机', meaning: '现有枪击同家族变体候选' },
  { id: 'reload-load', source: `${gunPack}/Foley/Load 1-1.wav`, family: 'reload-mechanism', targetRms: .05, fadeOutMs: 12, provenance: '工程已有Gun Sounds Pack Vol 1；包内未发现许可文档，仅本机', meaning: '保留本文件完整有效动作，不强行拼出完整装填流程' },
  { id: 'reload-pump', source: `${gunPack}/Foley/Pumping 3-1.wav`, family: 'reload-mechanism', targetRms: .05, fadeOutMs: 12, provenance: '工程已有Gun Sounds Pack Vol 1；包内未发现许可文档，仅本机', meaning: '保留本文件完整有效机械动作，不冒充盾防' },
  { id: 'mechanical-ui-candidate', source: `${gunPack}/Foley/Safety Off 1-1.wav`, family: 'mechanical-ui', targetRms: .04, fadeOutMs: 10, provenance: '工程已有Gun Sounds Pack Vol 1；包内未发现许可文档，仅本机', meaning: '机械确认声的本地候选，未试听，不宣称正式UI音色通过' },
  { id: 'explosion-small', source: `${gunPack}/Bonus/Explosion Small 1-1.wav`, family: 'explosion-small', targetRms: .05, fadeOutMs: 25, maxSeconds: 2, provenance: '工程已有Gun Sounds Pack Vol 1；包内未发现许可文档，仅本机', meaning: '仅用于实际爆炸语义，不用于任意技能落地' },
  { id: 'metal-impact-heavy', source: `${feelSamples}/Impacts/ImpactMetal1.wav`, family: 'metal-impact', targetRms: .055, fadeOutMs: 20, provenance: '工程已有Feel/NiceVibrations示例；第三方原始媒体授权未核实，仅本机', meaning: '保留完整金属撞击尾音的格挡候选，未试听，不宣称盾牌专属录音' },
  { id: 'metal-bullet-impact', source: `${feelSamples}/Impacts/BulletMetal1.wav`, family: 'metal-impact', targetRms: .055, fadeOutMs: 15, provenance: '工程已有Feel/NiceVibrations示例；第三方原始媒体授权未核实，仅本机', meaning: '既有BulletMetal短金属撞击候选，未试听，不宣称盾牌专属录音' },
];
const policy = { threshold: .001, preRollMs: 2, postRollMs: 15, fadeInMs: 2, peakCeiling: .75, preserveSampleRate: true, preserveChannels: true, outputBits: 16 };
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const round = (value, places = 7) => Number(value.toFixed(places));

function readWav(file) {
  const bytes = fs.readFileSync(file);
  if (bytes.toString('ascii', 0, 4) !== 'RIFF' || bytes.toString('ascii', 8, 12) !== 'WAVE') throw new Error(`Unsupported RIFF/WAVE: ${file}`);
  let format = null, data = null;
  const chunks = [];
  for (let cursor = 12; cursor + 8 <= bytes.length;) {
    const id = bytes.toString('ascii', cursor, cursor + 4);
    const size = bytes.readUInt32LE(cursor + 4);
    const offset = cursor + 8;
    if (offset + size > bytes.length) throw new Error(`Truncated ${id} chunk: ${file}`);
    chunks.push({ id, size });
    if (id === 'fmt ') {
      if (size < 16) throw new Error(`Invalid fmt chunk: ${file}`);
      let codec = bytes.readUInt16LE(offset);
      if (codec === 65534) {
        if (size < 40) throw new Error(`Invalid extensible fmt chunk: ${file}`);
        codec = bytes.readUInt16LE(offset + 24);
      }
      format = { codec, channels: bytes.readUInt16LE(offset + 2), sampleRate: bytes.readUInt32LE(offset + 4), blockAlign: bytes.readUInt16LE(offset + 12), bits: bytes.readUInt16LE(offset + 14) };
    }
    if (id === 'data' && data === null) data = { offset, size };
    cursor = offset + size + (size % 2);
  }
  if (!format || !data || !format.channels || !format.sampleRate) throw new Error(`Missing wave format/data: ${file}`);
  const width = format.bits / 8;
  if (!Number.isInteger(width) || format.blockAlign !== width * format.channels || data.size % format.blockAlign !== 0) throw new Error(`Invalid PCM alignment: ${file}`);
  const frames = data.size / format.blockAlign;
  const samples = new Float64Array(frames * format.channels);
  for (let index = 0; index < samples.length; index++) {
    const offset = data.offset + index * width;
    let value;
    if (format.codec === 1 && format.bits === 8) value = (bytes[offset] - 128) / 128;
    else if (format.codec === 1 && format.bits === 16) value = bytes.readInt16LE(offset) / 32768;
    else if (format.codec === 1 && format.bits === 24) value = bytes.readIntLE(offset, 3) / 8388608;
    else if (format.codec === 1 && format.bits === 32) value = bytes.readInt32LE(offset) / 2147483648;
    else if (format.codec === 3 && format.bits === 32) value = bytes.readFloatLE(offset);
    else if (format.codec === 3 && format.bits === 64) value = bytes.readDoubleLE(offset);
    else throw new Error(`Unsupported audio codec ${format.codec}/${format.bits}: ${file}`);
    if (!Number.isFinite(value)) throw new Error(`Nonfinite input sample: ${file}`);
    samples[index] = value;
  }
  return { bytes, format, chunks, frames, samples };
}

function measure(samples, channels, sampleRate) {
  let sum = 0, peak = 0, first = samples.length / channels, last = -1;
  for (let index = 0; index < samples.length; index++) {
    const amplitude = Math.abs(samples[index]);
    sum += samples[index] * samples[index];
    peak = Math.max(peak, amplitude);
    if (amplitude >= policy.threshold) { first = Math.min(first, Math.floor(index / channels)); last = Math.max(last, Math.floor(index / channels)); }
  }
  return { peak, rms: Math.sqrt(sum / Math.max(1, samples.length)), firstActiveFrame: last < 0 ? null : first, lastActiveFrame: last < 0 ? null : last, onsetMs: last < 0 ? null : first / sampleRate * 1000 };
}

function prepare(entry) {
  const wav = readWav(entry.source);
  const { channels, sampleRate } = wav.format;
  const original = measure(wav.samples, channels, sampleRate);
  if (original.firstActiveFrame === null || original.peak === 0) throw new Error(`No usable source audio: ${entry.source}`);
  const startFrame = Math.max(0, original.firstActiveFrame - Math.round(policy.preRollMs / 1000 * sampleRate));
  const wantedEnd = Math.min(wav.frames, original.lastActiveFrame + 1 + Math.round(policy.postRollMs / 1000 * sampleRate));
  const endFrame = Math.min(wantedEnd, entry.maxSeconds ? startFrame + Math.floor(entry.maxSeconds * sampleRate) : wantedEnd);
  const frames = endFrame - startFrame;
  const trimmed = wav.samples.slice(startFrame * channels, endFrame * channels);
  const fadeInFrames = Math.min(frames, Math.max(2, Math.round(policy.fadeInMs / 1000 * sampleRate)));
  const fadeOutFrames = Math.min(frames, Math.max(2, Math.round(entry.fadeOutMs / 1000 * sampleRate)));
  for (let frame = 0; frame < frames; frame++) {
    const fadeIn = frame < fadeInFrames ? frame / (fadeInFrames - 1) : 1;
    const fadeOut = frame >= frames - fadeOutFrames ? (frames - 1 - frame) / (fadeOutFrames - 1) : 1;
    for (let channel = 0; channel < channels; channel++) trimmed[frame * channels + channel] *= Math.min(fadeIn, fadeOut);
  }
  const faded = measure(trimmed, channels, sampleRate);
  if (faded.rms <= 0 || faded.peak <= 0) throw new Error(`Empty after curation: ${entry.id}`);
  return { entry, wav, original, startFrame, endFrame, frames, trimmed, faded, maxRmsAtCeiling: faded.rms * policy.peakCeiling / faded.peak };
}

function encode(samples, sampleRate, channels) {
  const bytes = Buffer.alloc(44 + samples.length * 2);
  bytes.write('RIFF', 0); bytes.writeUInt32LE(bytes.length - 8, 4); bytes.write('WAVEfmt ', 8);
  bytes.writeUInt32LE(16, 16); bytes.writeUInt16LE(1, 20); bytes.writeUInt16LE(channels, 22);
  bytes.writeUInt32LE(sampleRate, 24); bytes.writeUInt32LE(sampleRate * channels * 2, 28);
  bytes.writeUInt16LE(channels * 2, 32); bytes.writeUInt16LE(16, 34); bytes.write('data', 36); bytes.writeUInt32LE(samples.length * 2, 40);
  const ceiling = Math.floor(policy.peakCeiling * 32768);
  for (let index = 0; index < samples.length; index++) {
    const sample = Math.round(samples[index] * 32768);
    if (Math.abs(sample) > ceiling) throw new Error(`Unexpected peak overflow at sample ${index}`);
    bytes.writeInt16LE(sample, 44 + index * 2);
  }
  return bytes;
}

function writePreserving(file, bytes) {
  if (fs.existsSync(file)) {
    if (hash(fs.readFileSync(file)) !== hash(bytes)) throw new Error(`Existing output differs; preserving it: ${file}`);
    return;
  }
  fs.writeFileSync(file, bytes, { flag: 'wx' });
}

const prepared = clips.map(prepare);
const familyTargets = {};
for (const item of prepared) familyTargets[item.entry.family] = Math.min(familyTargets[item.entry.family] ?? Infinity, item.entry.targetRms, item.maxRmsAtCeiling);
fs.mkdirSync(outputDirectory, { recursive: true });
const records = [];
for (const item of prepared) {
  const { entry, wav, trimmed, faded, frames, startFrame, endFrame } = item;
  const { channels, sampleRate } = wav.format;
  const target = familyTargets[entry.family];
  const gain = Math.min(target / faded.rms, policy.peakCeiling / faded.peak);
  const normalized = Float64Array.from(trimmed, sample => sample * gain);
  const output = path.join(outputDirectory, `${entry.id}.wav`);
  const bytes = encode(normalized, sampleRate, channels);
  writePreserving(output, bytes);
  const decoded = readWav(output);
  const actual = measure(decoded.samples, channels, sampleRate);
  if (decoded.format.channels !== channels || decoded.format.sampleRate !== sampleRate || decoded.frames !== frames) throw new Error(`Roundtrip format failed: ${entry.id}`);
  if (actual.peak > policy.peakCeiling || !Number.isFinite(actual.rms) || actual.firstActiveFrame === null) throw new Error(`Roundtrip peak/content failed: ${entry.id}`);
  if (decoded.samples[0] !== 0 || decoded.samples[decoded.samples.length - 1] !== 0) throw new Error(`Fade edge failed: ${entry.id}`);
  if (hash(fs.readFileSync(entry.source)) !== hash(wav.bytes)) throw new Error(`Original changed during curation: ${entry.id}`);
  records.push({
    id: entry.id, family: entry.family, meaning: entry.meaning, audition: 'NOT_AUDITIONED', redistribution: 'RESTRICTED_LOCAL_ONLY', provenance: entry.provenance,
    source: { absolutePath: path.resolve(entry.source), sha256: hash(wav.bytes), bytes: wav.bytes.length, format: wav.format, chunks: wav.chunks, durationSeconds: round(wav.frames / sampleRate), peak: round(item.original.peak), rms: round(item.original.rms), onsetMs: round(item.original.onsetMs, 3) },
    trim: { thresholdLinear: policy.threshold, originalStartFrame: startFrame, originalEndFrameExclusive: endFrame, removedLeadingSeconds: round(startFrame / sampleRate), removedTrailingSeconds: round((wav.frames - endFrame) / sampleRate), maxDurationApplied: entry.maxSeconds !== undefined && endFrame < Math.min(wav.frames, item.original.lastActiveFrame + 1 + Math.round(policy.postRollMs / 1000 * sampleRate)), fadeInMs: policy.fadeInMs, fadeOutMs: entry.fadeOutMs },
    gain: { linear: round(gain), decibels: round(20 * Math.log10(gain), 4), desiredFamilyRms: entry.targetRms, actualFamilyRmsTarget: round(target), peakCeiling: policy.peakCeiling, familyTargetReducedByPeakCeiling: target < entry.targetRms - 1e-8 },
    output: { absolutePath: path.resolve(output), sha256: hash(bytes), bytes: bytes.length, channels, sampleRate, bits: 16, durationSeconds: round(frames / sampleRate), peak: round(actual.peak), rms: round(actual.rms), onsetMs: round(actual.onsetMs, 3), firstFrameZero: true, lastFrameZero: true },
  });
}
const manifest = { schemaVersion: 1, manifestVersion: 2, task: 'AUDIO-SFXPASS-20261025', method: 'Existing PCM only: threshold trim with preroll/postroll, linear edge fades, one scalar gain per clip; no synthesis/resample/pitch/time-stretch/limiter', policy, scriptSha256: hash(fs.readFileSync(fileURLToPath(import.meta.url))), allOriginalsUnchanged: true, allOutputPeaksAtOrBelowCeiling: true, audition: 'NOT_AUDITIONED', redistribution: 'ALL_DERIVED_MEDIA_RESTRICTED_LOCAL_ONLY', records };
const manifestPath = path.join(outputDirectory, 'manifest.json');
const manifestBytes = Buffer.from(JSON.stringify(manifest, null, 2) + '\n', 'utf8');
if (fs.existsSync(manifestPath) && hash(fs.readFileSync(manifestPath)) !== hash(manifestBytes)) {
  const previousBytes = fs.readFileSync(manifestPath);
  const previous = JSON.parse(previousBytes.toString('utf8'));
  if (previous.task !== manifest.task || previous.schemaVersion !== 1 || !Array.isArray(previous.records)) throw new Error('Refusing to replace unknown manifest');
  for (const old of previous.records) {
    const current = records.find(record => record.id === old.id);
    if (!current || old.output.sha256 !== current.output.sha256 || old.source.sha256 !== current.source.sha256) throw new Error(`Existing curated PCM changed: ${old.id}`);
  }
  writePreserving(path.join(outputDirectory, `manifest-v${previous.manifestVersion ?? 1}.json`), previousBytes);
  fs.writeFileSync(manifestPath, manifestBytes);
} else writePreserving(manifestPath, manifestBytes);
console.log(JSON.stringify({ manifestPath, outputCount: records.length, allOriginalsUnchanged: true, clips: records.map(record => ({ id: record.id, seconds: record.output.durationSeconds, onsetMs: record.output.onsetMs, gainDb: record.gain.decibels, peak: record.output.peak, rms: record.output.rms })) }, null, 2));
