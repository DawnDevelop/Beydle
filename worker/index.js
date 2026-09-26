// Cloudflare Worker for beydle.com. Static files are served straight from output/wwwroot; this script only runs
// for requests no file matches. It accepts POST /api/event, one anonymous event per guess, and writes it to
// Workers Analytics Engine (dataset beydle_events). Nothing identifying is stored and nothing is ever read back,
// so the endpoint cannot be used to look up today's answer.
//
// Data point layout (queried as index1, blob1..blob5, double1):
//   index1  date (yyyy-MM-dd, Europe/Berlin)   blob1 mode (daily | practice)   blob2 round (1 | 2)
//   blob3   guessed blade id                   blob4 solved | miss             blob5 xtreme | normal
//   double1 guess number within the round
const MODES = ["daily", "practice"];
const DAY_MS = 86400000;
let bladeIds;

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
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
