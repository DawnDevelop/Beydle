// Cloudflare Worker for beydle.com. Static files are served straight from output/wwwroot; this script only runs
// for requests no file matches. It serves the app shell for the meta pages (/meta and one page per blade at
// /meta/{slug}) with their own titles, lists the blade pages in /sitemap-meta.xml, and it accepts POST /api/event,
// one anonymous event per guess, and writes it to Workers Analytics Engine (dataset beydle_events). Nothing identifying is stored and nothing is ever read back, so the endpoint cannot be used to
// look up today's answer.
//
// Data point layout (queried as index1, blob1..blob5, double1):
//   index1  date (yyyy-MM-dd, Europe/Berlin)   blob1 mode (daily | practice)   blob2 round (1 | 2)
//   blob3   guessed blade id                   blob4 solved | miss             blob5 xtreme | normal
//   double1 guess number within the round
const MODES = ["daily", "practice"];
const DAY_MS = 86400000;
let bladeIds;
let bladePages; // slug -> blade name, from data/meta/blade-pages.json (written by meta/BeybladeMeta.Indexer)
const cards = new Map(); // slug -> whether img/og/meta/{slug}.jpg exists (written by tools/build-og-cards.js)

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (url.pathname === "/meta") return metaPage(await env.ASSETS.fetch(new URL("/", url)), META);
    if (url.pathname.startsWith("/meta/")) {
      const slug = url.pathname.slice("/meta/".length);
      const name = (await pages(env, url)).get(slug);
      if (!name) return new Response("Not found", { status: 404 });
      return metaPage(await env.ASSETS.fetch(new URL("/", url)), bladeHead(slug, name, await hasCard(env, url, slug)));
    }
    if (url.pathname === "/sitemap-meta.xml") return sitemap(await pages(env, url));
    if (url.pathname !== "/api/event") return new Response("Not found", { status: 404 });
    if (request.method !== "POST") return new Response(null, { status: 405, headers: { Allow: "POST" } });
    if (Number(request.headers.get("content-length") ?? 0) > 1024) return new Response(null, { status: 413 });

    let e;
    try { e = await request.json(); } catch { return new Response(null, { status: 400 }); }

    bladeIds ??= new Set((await (await env.ASSETS.fetch(new URL("/data/blades.json", url))).json()).map((b) => b.id));
    if (!isValid(e)) return new Response(null, { status: 400 });

    env.EVENTS.writeDataPoint({
      indexes: [e.date],
      blobs: [e.mode, String(e.round), e.guess, e.solved ? "solved" : "miss", e.xtreme ? "xtreme" : "normal"],
      doubles: [e.number],
    });
    return new Response(null, { status: 204 });
  },
};

const META = {
  title: "Beyblade X tournament meta - Beydle",
  description: "The Beyblade X blades, combos, ratchets, bits and decks that win tournaments, ranked from top-3 finishes at World Beyblade Organization events.",
  url: "https://beydle.com/meta",
  heading: "Beyblade X tournament meta",
  link: 'Also on Beydle: <a href="/">the daily Beyblade X guessing game</a>.',
  image: "https://beydle.com/og-image-meta.png",
  imageAlt: "Beydle Meta logo with a podium and the text: What wins Beyblade X tournaments",
};

// A blade page's preview is its own card when one has been rendered, otherwise the meta page's image.
function bladeHead(slug, name, card) {
  return {
    title: `${name}: best combos and tournament results - Beydle`,
    description: `The best ${name} combos, ratchets, bits and decks in Beyblade X, ranked from top-3 finishes at World Beyblade Organization events.`,
    url: `https://beydle.com/meta/${slug}`,
    heading: `${name} in the Beyblade X meta`,
    link: 'See <a href="/meta">every blade in the tournament meta</a>, or play <a href="/">the daily Beyblade X guessing game</a>.',
    image: card ? `https://beydle.com/img/og/meta/${slug}.jpg` : META.image,
    imageAlt: card ? `${name} with the text: Beydle Meta, best combos and tournament results` : META.imageAlt,
  };
}

async function hasCard(env, url, slug) {
  if (!cards.has(slug)) cards.set(slug, (await env.ASSETS.fetch(new URL(`/img/og/meta/${slug}.jpg`, url), { method: "HEAD" })).ok);
  return cards.get(slug);
}

async function pages(env, url) {
  bladePages ??= new Map((await (await env.ASSETS.fetch(new URL("/data/meta/blade-pages.json", url))).json()).map((p) => [p.slug, p.name]));
  return bladePages;
}

function sitemap(pages) {
  const urls = [...pages.keys()].map((slug) => `  <url><loc>https://beydle.com/meta/${slug}</loc></url>`).join("\n");
  return new Response(`<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${urls}\n</urlset>\n`,
    { headers: { "Content-Type": "application/xml; charset=utf-8" } });
}

// index.html describes the game; give crawlers and link previews each meta page's own title, text and address.
function metaPage(shell, head) {
  const set = (attr, value) => ({ element: (e) => e.setAttribute(attr, value) });
  return new HTMLRewriter()
    .on("title", { element: (e) => e.setInnerContent(head.title) })
    .on('meta[name="description"], meta[property="og:description"], meta[name="twitter:description"]', set("content", head.description))
    .on('meta[property="og:title"], meta[name="twitter:title"]', set("content", head.title))
    .on('link[rel="canonical"]', set("href", head.url))
    .on('meta[property="og:url"]', set("content", head.url))
    .on('meta[property="og:image"], meta[name="twitter:image"]', set("content", head.image))
    .on('meta[property="og:image:alt"]', set("content", head.imageAlt))
    .on(".boot-intro h1", { element: (e) => e.setInnerContent(head.heading) })
    .on(".boot-desc", { element: (e) => e.setInnerContent(head.description) })
    .on(".boot-link", { element: (e) => e.setInnerContent(head.link, { html: true }) })
    .transform(shell);
}

// Rejects anything the game cannot produce. The date may differ from UTC by a day (Berlin midnight, clock skew).
function isValid(e) {
  return e !== null && typeof e === "object"
    && typeof e.date === "string" && /^\d{4}-\d{2}-\d{2}$/.test(e.date) && Math.abs(Date.parse(e.date) - Date.now()) < 2 * DAY_MS
    && MODES.includes(e.mode)
    && (e.round === 1 || e.round === 2)
    && bladeIds.has(e.guess)
    && Number.isInteger(e.number) && e.number >= 1 && e.number <= 500
    && typeof e.solved === "boolean"
    && typeof e.xtreme === "boolean";
}
