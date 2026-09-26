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
// The round-2 blade of day n (DailyPicker.ImageAt): the sequence at n + IMAGE_OFFSET, unless that is the day's round-1
// blade. Then a blade from half a cycle further on stands in, avoiding yesterday's image and tomorrow's regular one.
function imageFor(n, firstId, yesterdayId, at) {
  const count = blades.length, k = n + IMAGE_OFFSET, regular = at(k);
  if (regular.id !== firstId) return regular;
  const tomorrowId = at(k + 1).id, start = k + Math.floor(count / 2);
  let any = null, fallback = null;
  for (let j = start; j < start + 2 * count; j++) {
    const candidate = at(j);
    if (candidate.id === firstId) continue;
    any ??= candidate;
    if (candidate.id === yesterdayId) continue;
    if (candidate.id !== tomorrowId) return candidate;
    fallback ??= candidate;
  }
  return fallback ?? any;
}
const override = [new Map(), new Map()]; // round -> sequence position -> blade, set by catchUp()
const seqAt = (round, k) => override[round].get(k) ?? pickAt(k);
function pickDay(n, yesterdayImageId) {
  const first = seqAt(0, n);
  const image = imageFor(n, first.id, yesterdayImageId, (k) => seqAt(1, k));
  const dateKey = key(fromDayNumber(n));
  return { hashes: [hashFor(dateKey, first.id), hashFor(dateKey, image.id)], imageId: image.id };
}

// Cycles are counted from EPOCH with the pool size as their length, so adding or removing a blade moves every future
// cycle boundary: the regular generator would bring back blades shown days ago while others wait for months. When the
// kept days are not what the regular generator shows, the days up to the next cycle boundary instead show the blades
// not seen for longest (new blades first), each once, so the `count` days before the boundary hold as many different
// blades as possible. Regular cycles follow from the boundary on, so the file's tail still matches the C# generator.
// Rerunning on a later day reproduces the same days, as they depend only on the history and the boundary.
function catchUp(round, history) {
  const count = blades.length;
  const p0 = startN + (round === 0 ? 0 : IMAGE_OFFSET);
  const boundary = (Math.floor(p0 / count) + 1) * count;
  const length = boundary - p0;
  const windowDays = [];
  for (let n = startN - (count - length); n < startN; n++) if (history[round].has(n)) windowDays.push(n);
  const regular = (n) => (round === 0 ? pickAt(n) : imageFor(n, history[0].get(n), history[1].get(n - 1), pickAt)).id;
  if (windowDays.every((n) => history[round].get(n) === regular(n))) return;

  const seen = new Set(windowDays.map((n) => history[round].get(n)));
  const last = new Map();
  for (const [n, id] of history[round]) last.set(id, Math.max(last.get(id) ?? -1, n));
  const rank = (id) => crypto.createHash("sha256").update(`${SALT}|catch-up|${boundary}|${id}`).digest("hex");
  const byRank = (a, b) => (rank(a) < rank(b) ? -1 : 1);
  const chosen = blades.map((b) => b.id).filter((id) => !seen.has(id))
    .sort((a, b) => (last.get(a) ?? -1) - (last.get(b) ?? -1) || byRank(a, b))
    .slice(0, length);
  // Order them as they come up in the next regular cycle, so each blade's catch-up day and next showing are about
  // `length` days apart. The order looks random, so a newly added blade is not predictably tomorrow's answer.
  const next = permutation(count, boundary / count).map((i) => blades[i].id);
  chosen.sort((a, b) => next.indexOf(a) - next.indexOf(b));
  // Never the same blade on two consecutive days, at either end of the catch-up.
  if (length > 1 && chosen[0] === history[round].get(startN - 1)) [chosen[0], chosen[1]] = [chosen[1], chosen[0]];
  if (length > 1 && chosen[length - 1] === pickAt(boundary).id) [chosen[length - 2], chosen[length - 1]] = [chosen[length - 1], chosen[length - 2]];
  chosen.forEach((id, i) => override[round].set(p0 + i, blades.find((b) => b.id === id)));
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
const history = [new Map(), new Map()]; // round -> day number -> blade id shown on a kept day
for (const [k, v] of Object.entries(schedule)) {
  const byHash = new Map(blades.map((b) => [hashFor(k, b.id), b.id]));
  v.forEach((h, round) => { if (byHash.has(h)) history[round].set(dayNumber(parse(k)), byHash.get(h)); });
}
catchUp(0, history);
catchUp(1, history);
let yesterdayImageId = history[1].get(startN - 1);
for (let n = startN; n <= endN; n++) {
  const day = pickDay(n, yesterdayImageId);
  schedule[key(fromDayNumber(n))] = day.hashes;
  yesterdayImageId = day.imageId;
  written++;
}

const sorted = Object.fromEntries(Object.entries(schedule).sort(([a], [b]) => (a < b ? -1 : 1)));
fs.writeFileSync(outPath, JSON.stringify(sorted, null, 0).replace(/],"/g, "],\n\"").replace("{", "{\n") + "\n");
console.log(`schedule: kept ${kept} past/current days, wrote ${written} days (${key(fromDayNumber(startN))} .. ${key(fromDayNumber(endN))})`);
