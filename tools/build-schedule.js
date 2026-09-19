// Generates wwwroot/data/schedule.json: a fixed mapping of date -> [hash of round-1 blade id, hash of round-2 blade id].
// Committing the schedule means adding or removing blades never changes a day that players may already be on.
//
// usage: node tools/build-schedule.js            # extend/refresh from tomorrow (Europe/Berlin) onwards, keep today and the past
//        node tools/build-schedule.js --from 2026-01-01   # regenerate everything from a date (only for a fresh start)
//
// The generator is a JavaScript port of Services/DailyPicker.cs (Pick / PickImage) and must stay in sync with it.
const fs = require("fs");
const path = require("path");
const crypto = require("crypto");

const root = path.join(__dirname, "..");
const blades = require(path.join(root, "wwwroot", "data", "blades.json"));
const outPath = path.join(root, "wwwroot", "data", "schedule.json");
const YEARS_AHEAD = 3;
// Must match Services/ScheduleHash.cs: first 32 hex chars of SHA-256("<salt>|<yyyy-MM-dd>|<blade id>").
const SALT = "beydle-schedule-2026";
const hashFor = (dateKey, id) => crypto.createHash("sha256").update(`${SALT}|${dateKey}|${id}`).digest("hex").slice(0, 32);
const IMAGE_OFFSET = 7919;
const EPOCH = Date.UTC(2026, 0, 1);

const dayNumber = (d) => Math.round((Date.UTC(d.y, d.m - 1, d.d) - EPOCH) / 86400000);
const fromDayNumber = (n) => { const t = new Date(EPOCH + n * 86400000); return { y: t.getUTCFullYear(), m: t.getUTCMonth() + 1, d: t.getUTCDate() }; };
const key = (d) => `${d.y}-${String(d.m).padStart(2, "0")}-${String(d.d).padStart(2, "0")}`;
const parse = (s) => { const [y, m, d] = s.split("-").map(Number); return { y, m, d }; };

function shuffle(count, seed) {
  const p = [...Array(count).keys()];
  let s = (Math.imul(seed, 0x9e3779b9) + 0x7f4a7c15) >>> 0;
  for (let i = count - 1; i > 0; i--) {
    s ^= (s << 13) >>> 0; s >>>= 0; s ^= s >>> 17; s ^= (s << 5) >>> 0; s >>>= 0;
    const j = s % (i + 1);
    [p[i], p[j]] = [p[j], p[i]];
  }
  return p;
}
function permutation(count, cycle) {
  const p = shuffle(count, cycle);
  const prevLast = shuffle(count, cycle - 1)[count - 1];
  if (p[0] === prevLast) [p[0], p[1]] = [p[1], p[0]];
  return p;
}
function pickAt(n) {
  const count = blades.length;
  const cycle = Math.floor(n / count);
  return blades[permutation(count, cycle)[n - cycle * count]];
}
function pickDay(n) {
  const first = pickAt(n);
  let k = n + IMAGE_OFFSET, image;
  do { image = pickAt(k++); } while (image.id === first.id);
  const dateKey = key(fromDayNumber(n));
  return [hashFor(dateKey, first.id), hashFor(dateKey, image.id)];
}

const todayBerlin = parse(new Intl.DateTimeFormat("en-CA", { timeZone: "Europe/Berlin", year: "numeric", month: "2-digit", day: "2-digit" }).format(new Date()));
const fromArg = process.argv.indexOf("--from");
const startN = fromArg >= 0 ? dayNumber(parse(process.argv[fromArg + 1])) : dayNumber(todayBerlin) + 1;
const endN = dayNumber(todayBerlin) + YEARS_AHEAD * 366;

const schedule = fs.existsSync(outPath) ? JSON.parse(fs.readFileSync(outPath, "utf8")) : {};
const ids = new Set(blades.map((b) => b.id));
let kept = 0, written = 0;
for (const [k, v] of Object.entries(schedule)) {
  if (dayNumber(parse(k)) >= startN) { delete schedule[k]; continue; }
  const known = new Set([...ids].map((id) => hashFor(k, id)));
  if (v.every((h) => known.has(h))) kept++; else console.warn("kept day does not match any current blade:", k);
}
for (let n = startN; n <= endN; n++) { schedule[key(fromDayNumber(n))] = pickDay(n); written++; }

const sorted = Object.fromEntries(Object.entries(schedule).sort(([a], [b]) => (a < b ? -1 : 1)));
fs.writeFileSync(outPath, JSON.stringify(sorted, null, 0).replace(/],"/g, "],\n\"").replace("{", "{\n") + "\n");
console.log(`schedule: kept ${kept} past/current days, wrote ${written} days (${key(fromDayNumber(startN))} .. ${key(fromDayNumber(endN))})`);
