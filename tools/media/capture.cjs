// Captures the README screenshots and demo GIF from a running, seeded Tally
// (see seed-demo.mjs). Needs Playwright and ffmpeg:
//   npm install --prefix "$TMP/pw" playwright && npx --prefix "$TMP/pw" playwright install chromium
//   NODE_PATH="$TMP/pw/node_modules" node tools/media/capture.cjs http://localhost:8080 docs/media
const { chromium } = require('playwright');
const { execFileSync } = require('node:child_process');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');

const base = process.argv[2] ?? 'http://localhost:8080';
const out = path.resolve(process.argv[3] ?? 'docs/media');
const only = process.argv[4]; // "shots" or "gif"
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

async function openAsSam(browser, options) {
  const context = await browser.newContext({ serviceWorkers: 'block', locale: 'en-GB', colorScheme: 'light', ...options });
  const page = await context.newPage();
  await page.goto(`${base}/profiles`);
  await page.getByRole('button', { name: /Sam/ }).waitFor();
  return { context, page };
}

async function pickSam(page) {
  await page.getByRole('button', { name: /Sam/ }).click();
  await page.waitForURL(/\/diary/);
  await page.locator('.meal').first().waitFor();
}

async function screenshots(browser, name, options) {
  const { context, page } = await openAsSam(browser, options);
  const shot = async (view) => {
    await sleep(400);
    await page.screenshot({ path: path.join(out, `${view}-${name}.png`) });
  };

  await shot('profiles');
  await pickSam(page);
  await shot('diary');

  const dinner = page.locator('.meal').filter({ hasText: 'Dinner' });
  await dinner.getByRole('link').click();
  await page.getByLabel('Search foods').fill('chicken breast');
  await page.locator('.food-row').first().waitFor();
  await sleep(600);
  await shot('search');

  await page.goto(`${base}/recipes`);
  await page.getByRole('link', { name: /Chicken and broccoli pasta/ }).click();
  await page.getByRole('heading', { name: /Chicken and broccoli pasta/ }).waitFor();
  await shot('recipe');

  await page.goto(`${base}/history`);
  await page.locator('.recharts-surface').first().waitFor();
  await sleep(1800); // let the chart finish animating
  await shot('history');

  await page.goto(`${base}/goals`);
  await page.getByRole('heading', { name: /goals/i }).first().waitFor();
  await shot('goals');

  await context.close();
}

// Lossless frames from page.screenshot with their real timings, so the GIF
// plays at the speed the flow really runs at and has no video artefacts.
async function gif(browser) {
  const framesDir = fs.mkdtempSync(path.join(os.tmpdir(), 'tally-frames-'));
  const { context, page } = await openAsSam(browser, { viewport: { width: 800, height: 600 } });

  // Screenshots don't show the mouse, so draw one.
  await page.addStyleTag({
    content: `#demo-cursor{position:fixed;z-index:99999;width:18px;height:18px;margin:-9px 0 0 -9px;border-radius:50%;
      background:rgba(31,111,92,.45);border:2px solid #1f6f5c;pointer-events:none;left:400px;top:520px}`,
  });
  const addCursor = () => page.evaluate(() => {
    if (document.getElementById('demo-cursor')) return;
    const cursor = document.createElement('div');
    cursor.id = 'demo-cursor';
    document.body.append(cursor);
    window.addEventListener('mousemove', (e) => { cursor.style.left = `${e.clientX}px`; cursor.style.top = `${e.clientY}px`; }, true);
  });
  await addCursor();

  const frames = [];
  let recording = true;
  const recorder = (async () => {
    while (recording) {
      const started = Date.now();
      const file = path.join(framesDir, `${String(frames.length).padStart(5, '0')}.png`);
      await page.screenshot({ path: file });
      frames.push({ file, at: started });
      await sleep(Math.max(0, 100 - (Date.now() - started)));
    }
  })();

  let mouse = { x: 400, y: 520 };
  const click = async (locator, pause = 500) => {
    await locator.scrollIntoViewIfNeeded();
    const box = await locator.boundingBox();
    const target = { x: box.x + box.width / 2, y: box.y + box.height / 2 };
    const steps = 10;
    for (let i = 1; i <= steps; i++) {
      await page.mouse.move(mouse.x + ((target.x - mouse.x) * i) / steps, mouse.y + ((target.y - mouse.y) * i) / steps);
      await sleep(28);
    }
    mouse = target;
    await sleep(180);
    await page.mouse.click(target.x, target.y);
    await sleep(pause);
  };
  const nav = (label) => page.getByRole('navigation', { name: 'Main' }).getByRole('link', { name: label });

  await sleep(700);
  await click(page.getByRole('button', { name: /Sam/ }), 1000);
  await page.locator('.meal').first().waitFor();

  // Log dinner: search, pick a food, set the amount.
  const dinner = page.locator('.meal').filter({ hasText: 'Dinner' });
  await click(dinner.getByRole('link'), 500);
  const search = page.getByLabel('Search foods');
  await click(search, 200);
  await search.pressSequentially('salmon grilled', { delay: 85 });
  await page.locator('.food-row').first().waitFor();
  await sleep(1000);
  await click(page.locator('.food-row').filter({ hasText: 'Salmon, wild, grilled' }).first(), 1100);
  await click(page.getByRole('button', { name: /^Add to / }).last(), 1500);

  // See the day, then the history and goals.
  await page.locator('.meal').first().waitFor();
  await page.mouse.wheel(0, 260);
  await sleep(1200);
  await click(nav('History'), 300);
  await page.locator('.recharts-surface').first().waitFor();
  await sleep(2400);
  await click(nav('Goals'), 1500);
  recording = false;
  await recorder;
  await context.close();

  const list = frames.map((frame, i) => {
    const next = frames[i + 1]?.at ?? frame.at + 1200;
    return `file '${frame.file}'\nduration ${((next - frame.at) / 1000).toFixed(3)}`;
  });
  list.push(`file '${frames.at(-1).file}'`);
  const listFile = path.join(framesDir, 'list.txt');
  fs.writeFileSync(listFile, list.join('\n'));

  execFileSync('ffmpeg', ['-y', '-loglevel', 'error', '-f', 'concat', '-safe', '0', '-i', listFile,
    '-vf', 'fps=10,split[a][b];[a]palettegen=max_colors=128:stats_mode=diff[p];[b][p]paletteuse=dither=none:diff_mode=rectangle',
    '-loop', '0', path.join(out, 'demo.gif')]);
  fs.rmSync(framesDir, { recursive: true });
  const seconds = (frames.at(-1).at - frames[0].at) / 1000;
  console.log(`demo.gif: ${frames.length} frames, ${seconds.toFixed(1)} s, ${(fs.statSync(path.join(out, 'demo.gif')).size / 1048576).toFixed(2)} MB`);
}

(async () => {
  fs.mkdirSync(out, { recursive: true });
  const browser = await chromium.launch();
  if (only !== 'gif') {
    await screenshots(browser, 'desktop', { viewport: { width: 1280, height: 800 } });
    await screenshots(browser, 'mobile', { viewport: { width: 375, height: 812 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true });
  }
  if (only !== 'shots') await gif(browser);
  await browser.close();
})().catch((error) => { console.error(error); process.exit(1); });
