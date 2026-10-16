const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('sharp');

const root = path.resolve(__dirname, '..');
const names = ['Blend', 'Paint', 'Sculpt', 'Create', 'Materials', 'Output', 'Terrain'];
const packageIcons = path.join(root, 'Packages/com.simpletools.simpleblend/Editor/Icons');
// Silhouette fixtures intentionally omit internal decoration: it must never escape the parent shapes.
const silhouettes = {
  Blend: '<path d="M3 9 16 3l13 6v14l-13 6-13-6Z"/>',
  Paint: '<rect x="3" y="3" width="26" height="26" rx="6"/>',
  Sculpt: '<path d="m4 24 5-15 13-5 6 20-12 5Z"/><circle cx="9" cy="9" r="3"/><circle cx="26" cy="24" r="3"/>',
  Create: '<path d="m3 11 11-6 11 6v12l-11 6-11-6Z"/><circle cx="24" cy="8" r="5"/>',
  Materials: '<rect x="3" y="9" width="20" height="20" rx="4"/><rect x="9" y="3" width="20" height="20" rx="4"/>',
  Output: '<rect x="3" y="14" width="26" height="15" rx="3"/><path d="M14 3h4v11l4-4 3 3-9 9-9-9 3-3 4 4Z"/>',
  Terrain: '<rect x="3" y="3" width="26" height="26" rx="5"/>'
};

function checkTopology(pixels, size, label) {
  const solid = Array.from({ length: size * size }, (_, i) => pixels[i * 4 + 3] >= 128);
  const neighbors = i => [i % size ? i - 1 : -1, i % size < size - 1 ? i + 1 : -1,
    i >= size ? i - size : -1, i < size * (size - 1) ? i + size : -1].filter(n => n >= 0);
  function flood(start, value) {
    const seen = new Set([start]);
    const queue = [start];
    for (let head = 0; head < queue.length; head++) {
      for (const n of neighbors(queue[head])) {
        if (!seen.has(n) && solid[n] === value) { seen.add(n); queue.push(n); }
      }
    }
    return seen.size;
  }
  const filled = solid.filter(Boolean).length;
  assert.ok(filled > 0, label + ': empty icon');
  assert.equal(flood(solid.indexOf(true), true), filled, label + ': disconnected fragment');
  assert.equal(flood(0, false), solid.length - filled, label + ': accidental transparent hole');
  for (let i = 0; i < size; i++) {
    for (const index of [i, (size - 1) * size + i, i * size, i * size + size - 1]) {
      assert.ok(pixels[index * 4 + 3] <= 4, label + ': silhouette touches the canvas edge');
    }
  }
}

(async () => {
  let checks = 0;
  for (const name of names) {
    const source = fs.readFileSync(path.join(__dirname, 'icon-sources', name + '.svg'));
    assert.deepEqual(fs.readFileSync(path.join(root, 'docs/prototypes/icons', name + '.svg')), source);
    const png = fs.readFileSync(path.join(packageIcons, name + '.png'));
    const expected = await sharp(source).resize(64, 64).png({ compressionLevel: 9, adaptiveFiltering: false }).toBuffer();
    assert.deepEqual(png, expected, name + ': stale package PNG; rebuild icons');
    checks += 2;
    const mask = Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 32 32">${silhouettes[name]}</svg>`);
    const expectedAlpha = await sharp(mask).ensureAlpha().raw().toBuffer();
    const actual = await sharp(png).ensureAlpha().raw().toBuffer();
    for (let i = 3; i < actual.length; i += 4) {
      // Shared antialiased edges can accumulate coverage when fills overlap.
      // Fully inside/outside pixels must still match the silhouette exactly.
      if (expectedAlpha[i] === 0 || expectedAlpha[i] === 255) {
        assert.ok(Math.abs(actual[i] - expectedAlpha[i]) <= 4, `${name}: gap/overflow at pixel ${(i - 3) / 4}`);
      }
    }
    checks++;
    for (const size of [16, 32, 64]) {
      for (const [kind, input] of [['SVG', source], ['PNG', png]]) {
        const pixels = await sharp(input).resize(size, size, { kernel: 'linear' }).ensureAlpha().raw().toBuffer();
        checkTopology(pixels, size, `${name} ${kind} ${size}px`);
        checks++;
      }
    }
  }
  console.log(`Passed ${checks} icon asset checks: exact generated copies, silhouette bounds, connected shapes and no transparent holes at 16/32/64px.`);
})().catch(error => { console.error(error); process.exitCode = 1; });
