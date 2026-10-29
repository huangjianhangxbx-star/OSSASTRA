// Offline document layout QA only. Uses a separate headless browser, never the user's tabs.
const fs = require('fs');
const path = require('path');
const {pathToFileURL} = require('url');
const {chromium} = require('playwright');
(async () => {
  const root = path.resolve(process.argv[2]);
  const browser = await chromium.launch({executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', headless: true});
  const reports = [];
  try {
    const documents = [
      ['03-tutorial', '地面组合逻辑教程.html', 'tutorial'],
      ['04-art-guide', '素材绘制规范.html', 'art-guide']
    ];
    if (fs.existsSync(path.join(root, '开始使用.html'))) documents.push(['', '开始使用.html', 'start']);
    for (const item of documents) {
      const qa = path.join(root, item[0] || '03-tutorial', 'qa'); fs.mkdirSync(qa, {recursive: true});
      for (const width of [1280, 720]) {
        const page = await browser.newPage({viewport: {width, height: 960}, deviceScaleFactor: 1});
        await page.route(/^https?:/, route => route.abort());
        await page.goto(pathToFileURL(path.join(root, item[0], item[1])).href, {waitUntil: 'load'});
        const result = await page.evaluate(() => {
          const ids = [...document.querySelectorAll('[id]')].map(node => node.id);
          return {
            imageCount: document.images.length,
            brokenImages: [...document.images].filter(image => image.naturalWidth === 0).map(image => image.src),
            duplicateIds: ids.filter((value, index) => ids.indexOf(value) !== index),
            horizontalOverflow: document.documentElement.scrollWidth > window.innerWidth + 1,
            localLinks: [...document.querySelectorAll('a[href]')].map(node => node.getAttribute('href')).filter(value => !value.startsWith('#')),
            title: document.title,
            chapters: [...document.querySelectorAll('main h2')].map(node => node.textContent)
          };
        });
        result.missingLocalLinks = result.localLinks.filter(value => !fs.existsSync(path.resolve(root, item[0], decodeURIComponent(value))));
        await page.screenshot({path: path.join(qa, item[2] + '-' + width + '.png')});
        await page.locator('main h2').nth(Math.min(4, result.chapters.length - 1)).scrollIntoViewIfNeeded();
        await page.screenshot({path: path.join(qa, item[2] + '-' + width + '-middle.png')});
        reports.push({document: item[1], width, ...result});
        await page.close();
      }
    }
  } finally { await browser.close(); }
  fs.writeFileSync(path.join(root, '03-tutorial', 'qa', 'layout-report.json'), JSON.stringify(reports, null, 2));
  if (reports.some(result => result.brokenImages.length || result.duplicateIds.length || result.horizontalOverflow || result.missingLocalLinks.length)) throw new Error('Document layout/media/link check failed: see layout-report.json');
  console.log(JSON.stringify({status: 'PASS', layouts: reports.length, widths: [1280, 720], brokenImages: 0, duplicateIds: 0, horizontalOverflow: false, missingLocalLinks: 0}));
})().catch(error => { console.error(error.stack); process.exitCode = 1; });
