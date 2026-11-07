import test from 'node:test';
import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';

// The generator must create actual editable occupancy, not declare success in a report.
test('native terrain keeps business row flat and contains two hills plus a real basin', async () => {
  const generator = new URL('./GenerateTerrain.mjs', import.meta.url);
  assert.ok(existsSync(generator), 'native terrain generator is missing');
  const { generateTerrain } = await import(generator.href);
  const result = await generateTerrain();
  assert.equal(result.asset.schema, 'xinghai-workshop-asset-1');
  assert.equal(result.asset.palette.length, 8);
  assert.equal(result.asset.voxelSize, 0.25);
  assert.ok(result.asset.cells.length < 250000);
  const heights = new Map();
  const occupied = new Set();
  for (const cell of result.asset.cells) {
    occupied.add(`${cell.x},${cell.y},${cell.z}`);
    const column = `${cell.x},${cell.y}`;
    heights.set(column, Math.max(heights.get(column) ?? -Infinity, (cell.z + 1) * 0.25));
  }
  const at = (x,z) => heights.get(`${Math.floor(x / 0.25)},${Math.floor(z / 0.25)}`);
  for (let x=7; x<=21; x+=0.25) assert.equal(at(x,-24), 0, 'world-prop support row must survive');
  assert.equal(at(-15,-10), 0, 'original building floor must stay flat');
  assert.equal(at(-24,-4), 0.75);
  assert.equal(at(24,12), 1.25);
  assert.equal(at(15,-16), -0.5, 'pond must remove ground volume');
  assert.equal(occupied.has('60,-64,-1'), false, 'pond cannot be a painted top over solid ground');
  assert.ok(result.surface.quads.length > 0);
  assert.ok(result.surface.quads.every(q => q.axis === 2 && q.sign === 1 || q.color === result.asset.sideColor));
  assert.equal(result.layout.pond.waterLevelM, -0.18);
  assert.ok(result.layout.statistics.footprintSquareMeters >= 2900 && result.layout.statistics.footprintSquareMeters <= 3250);
});
