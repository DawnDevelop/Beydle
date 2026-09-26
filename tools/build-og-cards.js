// Renders the link-preview card of every blade page (wwwroot/img/og/meta/{slug}.jpg, 1200x630): the blade's render,
// or the brand blade over a podium for CX blades and blades the game does not have, next to its name. The Worker
// points og:image at the card when one exists. Cards only show the name, so existing ones are kept and only blades
// that gained a page are rendered; pass --all to redo every card after changing the design.
//
// usage: npm install --no-save puppeteer-core@23 && node tools/build-og-cards.js [--all]
//        Needs Chrome or Edge; set CHROME_PATH if it is not at the default place for this OS.
//
// Which render belongs to a page comes from blade-pages.json (meta/BeybladeMeta.Indexer/BladePages.cs).
const fs = require("fs");
const path = require("path");
const puppeteer = require("puppeteer-core");

const root = path.join(__dirname, "..");
const wwwroot = path.join(root, "wwwroot");
const pages = require(path.join(wwwroot, "data", "meta", "blade-pages.json"));
const outDir = path.join(wwwroot, "img", "og", "meta");
const all = process.argv.includes("--all");
const chrome = process.env.CHROME_PATH ?? {
  win32: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
  darwin: "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
}[process.platform] ?? "/usr/bin/google-chrome";

const dataUri = (file, type) => `data:${type};base64,${fs.readFileSync(file).toString("base64")}`;
const escape = (s) => s.replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[c]);

const font = dataUri(path.join(wwwroot, "fonts", "inter-latin-v20.woff2"), "font/woff2");
const BLADE_PATH = "M24 7 Q30.14 2.87 35.79 5.42 L36.02 11.98 Q43.28 13.4 45.47 19.2 L41 24 Q45.13 30.14 42.58 35.79 L36.02 36.02 Q34.6 43.28 28.8 45.47 L24 41 Q17.86 45.13 12.21 42.58 L11.98 36.02 Q4.72 34.6 2.53 28.8 L7 24 Q2.87 17.86 5.42 12.21 L11.98 11.98 Q13.4 4.72 19.2 2.53 L24 7 Z M24 13.5 A10.5 10.5 0 1 0 24 34.5 A10.5 10.5 0 1 0 24 13.5 Z";
const brandBlade = `<svg viewBox="0 0 48 48"><defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#5b8cff"/><stop offset="1" stop-color="#b15bff"/></linearGradient></defs><path fill="url(#g)" fill-rule="evenodd" d="${BLADE_PATH}"/><circle cx="24" cy="24" r="7" fill="#fff" opacity=".35"/><circle cx="24" cy="24" r="3" fill="#fff"/></svg>`;
const podium = `<div class="blade">${brandBlade}</div><div class="podium"><div class="step s2">2</div><div class="step s1">1</div><div class="step s3">3</div></div>`;

const card = (name, s, render) => `<!DOCTYPE html><html><head><meta charset="utf-8"><style>
@font-face { font-family: Inter; font-weight: 400 800; src: url("${font}") format("woff2"); }
html, body { margin: 0; width: 1200px; height: 630px; overflow: hidden; }
body { font-family: Inter, sans-serif; color: #fff; box-sizing: border-box; padding: 0 70px 0 90px;
  display: flex; align-items: center; gap: 60px;
  background: radial-gradient(700px 520px at 22% 88%, #1f3060 0%, rgba(31,48,96,0) 70%), linear-gradient(180deg, #121a2d 0%, #0b0d14 100%); }
.art { position: relative; flex: 0 0 340px; height: 340px; display: grid; place-items: center; }
.render { width: 340px; height: 340px; object-fit: contain; filter: drop-shadow(0 18px 40px rgba(91,140,255,.45)); }
.podium { position: absolute; bottom: 10px; left: 20px; right: 20px; display: flex; align-items: flex-end; gap: 12px; }
.step { flex: 1; border-radius: 14px 14px 6px 6px; display: grid; place-items: start center; padding-top: 14px; font-weight: 800; font-size: 36px;
  color: rgba(11,13,20,.55); background: linear-gradient(180deg, #9aa4bb, #5d667d); box-shadow: inset 0 2px 0 rgba(255,255,255,.35), 0 18px 40px rgba(0,0,0,.45); }
.s1 { height: 160px; background: linear-gradient(180deg, #f3cf6a, #b8892a); } .s2 { height: 112px; } .s3 { height: 84px; background: linear-gradient(180deg, #d9a07a, #93603f); }
.blade { position: absolute; left: 50%; bottom: 182px; width: 136px; height: 136px; transform: translateX(-50%); filter: drop-shadow(0 0 26px rgba(91,140,255,.55)); }
.text { min-width: 0; flex: 1; }
.kicker { margin: 0 0 14px; font-size: 30px; font-weight: 800; letter-spacing: -.01em; }
.kicker span { background: linear-gradient(135deg, #5b8cff, #b15bff); -webkit-background-clip: text; background-clip: text; color: transparent; }
h1 { margin: 0; font-size: 104px; font-weight: 800; letter-spacing: -.03em; line-height: 1.02; }
h1 span { white-space: nowrap; } /* break between words only, never at a hyphen ("L-Drago") */
.l1 { margin: 22px 0 0; font-size: 34px; font-weight: 500; }
.l2 { margin: 12px 0 0; font-size: 28px; color: #aab2cc; white-space: nowrap; }
</style></head><body>
<div class="art">${render ? `<img class="render" src="${render}">` : podium}</div>
<div class="text">
  <p class="kicker">Beydle <span>Meta</span></p>
  <h1>${name.split(" ").map((w) => `<span>${escape(w)}</span>`).join(" ")}</h1>
  <p class="l1">Best combos &amp; tournament results</p>
  <p class="l2">beydle.com/meta/${s}</p>
</div></body></html>`;

(async () => {
  fs.mkdirSync(outDir, { recursive: true });
  const todo = pages.filter((p) => all || !fs.existsSync(path.join(outDir, `${p.slug}.jpg`)));
  if (todo.length === 0) return console.log("All blade pages have a preview card.");

  const browser = await puppeteer.launch({ executablePath: chrome, headless: true, args: ["--no-sandbox"] });
  const page = await browser.newPage();
  await page.setViewport({ width: 1200, height: 630 });
  for (const p of todo) {
    await page.setContent(card(p.name, p.slug, p.image && dataUri(path.join(wwwroot, p.image), "image/webp")), { waitUntil: "load" });
    await page.evaluate(async () => {
      await document.fonts.ready;
      const h1 = document.querySelector("h1"); // long names shrink until every word fits and they take two lines at most
      const tooBig = (size) => h1.scrollWidth > h1.clientWidth || h1.offsetHeight > size * 1.02 * 2 + 2;
      for (let size = 104; tooBig(size) && size > 48;) h1.style.fontSize = `${(size -= 4)}px`;
    });
    await page.screenshot({ path: path.join(outDir, `${p.slug}.jpg`), type: "jpeg", quality: 86 });
  }
  await browser.close();
  console.log(`Rendered ${todo.length} preview card(s) into ${path.relative(root, outDir)}.`);
})();
