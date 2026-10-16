const fs = require('node:fs');
const path = require('node:path');
const sharp = require('sharp');

const names = ['Blend', 'Paint', 'Sculpt', 'Create', 'Materials', 'Output', 'Terrain'];
const source = path.resolve(__dirname, 'icon-sources');
const destination = path.resolve(__dirname, '../Packages/com.simpletools.simpleblend/Editor/Icons');
const preview = path.resolve(__dirname, '../docs/prototypes/icons');
(async () => {
  fs.mkdirSync(destination, { recursive: true });
  fs.mkdirSync(preview, { recursive: true });
  for (const name of names) {
    await sharp(path.join(source, name + '.svg')).resize(64, 64).png({ compressionLevel: 9, adaptiveFiltering: false }).toFile(path.join(destination, name + '.png'));
    fs.copyFileSync(path.join(source, name + '.svg'), path.join(preview, name + '.svg'));
  }
  console.log(`Built ${names.length} transparent editor icons from original SVG sources.`);
})().catch(error => { console.error(error); process.exitCode = 1; });
