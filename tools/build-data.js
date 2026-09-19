// Builds wwwroot/data/blades.json from tools/blades_raw.json (extracted blade facts),
// tools/blade_meta.json (owner + release date reference data) and the verified combos below.
// usage: node tools/build-data.js
const fs = require("fs");
const path = require("path");
const crypto = require("crypto");
const root = path.join(__dirname, "..");
const raw = require("./blades_raw.json");
const meta = require("./blade_meta.json"); // reference data: { id: { owner, date } }
const outPath = path.join(root, "wwwroot", "data", "blades.json");
const imgDir = path.join(root, "wwwroot", "img", "blades");

// Image files are named by a salted hash of the blade id so the round-2 picture's URL does not spell out the answer.
const IMAGE_SALT = "beydle-2026";
const imageFile = (id) => crypto.createHash("sha1").update(IMAGE_SALT + ":" + id).digest("hex").slice(0, 16) + ".webp";

// Owner spellings vary across sources; map every variant to one canonical romanized name.
const OWNER_CANON = {
  "ekusu kurosu": "Jaxon Cross", "ekusu kurusu": "Jaxon Cross", "jaxon kross": "Jaxon Cross", "jaxon cross": "Jaxon Cross",
  "khrome ryugu": "Khrome Ryugu", "chrome ryugu": "Khrome Ryugu",
  "multi nanairo": "Multi Nana-iro", "multi nana-iro": "Multi Nana-iro", "persona/multi nana-iro": "Multi Nana-iro",
  "sigrid nana-iro": "Sigrid Nana-iro",
  "burn fujiwara": "Blaze Fujiwara", "blaze fujiwara": "Blaze Fujiwara",
  "meiko meiden": "Meiko Myoden", "meiko myoden": "Meiko Myoden",
  "king manju": "Titus Manju",
  "one kurosu": "One Cross", "two cross": "Two Cross", "five kurosu": "Five Cross", "six cross": "Six Cross",
  "eight kurosu": "Eight Cross", "nine cross": "Nine Cross", "three kurosu four kurosu": "Three & Four Cross",
};
// Blades not covered by the reference data: owner (null = no anime owner) and first TT release year.
const META_OVERRIDES = {
  // Owner and release year sourced separately (2026-09-19).
  "heavens-ring":   { year: 2026, owner: "Seven Cross" },
  "shelter-drake":  { year: 2025, owner: "Ciel Kaminari" },
  "ptera-swing":    { year: 2024, owner: null },   // only an unnamed Shadow Pro in the anime
  "unicorn-delta":  { year: 2026, owner: "Kiwami Miyazukae" },
  "glory-valkyrie": { year: 2026, owner: "One Cross" },
  "bear-scratch":   { year: 2024, owner: null },   // only an unnamed Shadow Pro in the anime
  "shinobi-knife":  { year: 2024, owner: "Sheer Kamikage" },
  "goat-tackle":    { year: 2025, owner: null },
  "shark-gill":     { year: 2025, owner: null },
  "phoenix-flare":  { year: 2026, owner: "Blaze Fujiwara" },
  "leon-fang":      { year: 2025, owner: "Line Shindo" },
  // Covered by the reference data but without an owner field.
  "mammoth-tusk":   { year: 2024, owner: null },
  "tyranno-roar":   { year: 2025, owner: "Rex Jura" },
  "viper-tail":     { year: 2023, owner: "Toguro Okunaga" },
  "wyvern-hover":   { year: 2025, owner: null },
};
const canonOwner = (o) => {
  if (!o) return null;
  const k = o.trim().toLowerCase();
  if (OWNER_CANON[k]) return OWNER_CANON[k];
  return o.trim().toLowerCase().replace(/(^|[\s-])\S/g, (c) => c.toUpperCase());
};

// X-Over Project blades (old-generation remakes) are excluded .
const XOVER = new Set([
  "Dranzer Spiral", "Driger Slash", "Dragoon Storm", "Draciel Shield", "Spriggan S",
  "Lightning L-Drago", "Victory Valkyrie", "Storm Pegasis", "Xeno Xcalibur", "Rock Leone", "Valkyrie Volt",
]);

// bit abbreviation -> [full name, type]
const BITS = {
  F: ["Flat", "Attack"], T: ["Taper", "Attack"], B: ["Ball", "Stamina"], D: ["Dot", "Defense"],
  N: ["Needle", "Defense"], HN: ["High Needle", "Defense"], LF: ["Low Flat", "Attack"], P: ["Point", "Balance"],
  O: ["Orb", "Stamina"], S: ["Spike", "Defense"], DS: ["Disk Spike", "Defense"], R: ["Rush", "Attack"],
  HT: ["High Taper", "Balance"], GF: ["Gear Flat", "Attack"], GB: ["Gear Ball", "Stamina"], GP: ["Gear Point", "Balance"],
  GN: ["Gear Needle", "Defense"], U: ["Unite", "Balance"], C: ["Cyclone", "Attack"], TP: ["Trans Point", "Balance"],
  E: ["Elevate", "Attack"], M: ["Merge", "Attack"], A: ["Accel", "Attack"], H: ["Hexa", "Defense"],
  DB: ["Disk Ball", "Stamina"], FB: ["Free Ball", "Stamina"], L: ["Level", "Attack"], BS: ["Bound Spike", "Defense"],
  LR: ["Low Rush", "Attack"], UN: ["Under Needle", "Defense"], Z: ["Zap", "Attack"], UF: ["Under Flat", "Attack"],
  J: ["Jolt", "Attack"], V: ["Vortex", "Attack"], LO: ["Low Orb", "Stamina"], W: ["Wedge", "Defense"],
  K: ["Kick", "Balance"], GR: ["Gear Rush", "Attack"], Tr: ["Turbo", "Balance"], WB: ["Wall Ball", "Stamina"],
  TK: ["Trans Kick", "Balance"], Op: ["Operate", "Defense"], FF: ["Free Flat", "Attack"], I: ["Ignition", "Attack"],
  Nr: ["Narrow", "Stamina"], Y: ["Yielding", "Stamina"], Q: ["Quake", "Attack"], MN: ["Metal Needle", "Defense"],
  G: ["Glide", "Stamina"], RA: ["Rubber Accel", "Attack"], WW: ["Wall Wedge", "Defense"], GU: ["Gear Unite", "Balance"],
};

// raw blade name -> { display (Takara Tomy) name, code, ratchet, bit abbr, extra aliases }
// "INT" ratchet = ratchet integrated into blade or bit.
const COMBOS = {
  "Aero Pegasus":     { code: "UX-00", ratchet: "3-70", bit: "A" },
  "Cobalt Drake":     { code: "BX-00", ratchet: "4-60", bit: "F" },
  "Cobalt Dragoon":   { code: "BX-34", ratchet: "2-60", bit: "C" },
  "Dran Buster":      { code: "UX-01", ratchet: "1-60", bit: "A" },
  "Dran Dagger":      { code: "BX-20", ratchet: "4-60", bit: "R" },
  "Dran Sword":       { code: "BX-01", ratchet: "3-60", bit: "F" },
  "Impact Drake":     { code: "UX-11", ratchet: "9-60", bit: "LR" },
  "Meteor Dragoon":   { code: "UX-17", ratchet: "3-70", bit: "J" },
  "Phoenix Feather":  { code: "BX-00", ratchet: "3-60", bit: "F" },
  "Phoenix Wing":     { code: "BX-23", ratchet: "9-60", bit: "GF" },
  "Samurai Saber":    { code: "UX-09", ratchet: "2-70", bit: "L" },
  "Shark Edge":       { code: "BX-14", ratchet: "3-60", bit: "LF" },
  "Shark Scale":      { code: "UX-15", ratchet: "4-50", bit: "UF" },
  "Tyranno Beat":     { code: "BX-31", ratchet: "4-70", bit: "Q" },
  "Black Shell":      { code: "BX-35", ratchet: "4-60", bit: "D" },
  "Golem Rock":       { code: "UX-13", ratchet: "1-60", bit: "UN" },
  "Knight Lance":     { code: "BX-13", ratchet: "4-80", bit: "HN" },
  "Knight Mail":      { code: "UX-10", ratchet: "3-85", bit: "BS" },
  "Knight Shield":    { code: "BX-04", ratchet: "3-80", bit: "N" },
  "Leon Crest":       { code: "UX-06", ratchet: "7-60", bit: "GN" },
  "Mummy Curse":      { code: "UX-18", ratchet: "7-55", bit: "W" },
  "Rhino Horn":       { code: "BX-19", ratchet: "3-80", bit: "S" },
  "Sphinx Cowl":      { code: "BX-27", ratchet: "9-80", bit: "GN", aliases: ["Sphynx Cowl"] },
  "Tricera Press":    { code: "BX-44", ratchet: "M-85", bit: "BS" },
  "Wyvern Hover":     { code: "UX-00", ratchet: "2-80", bit: "GN" },
  "Shinobi Shadow":   { code: "UX-05", ratchet: "1-80", bit: "MN" },
  "Clock Mirage":     { code: "UX-16", ratchet: "9-65", bit: "B" },
  "Ghost Circle":     { code: "UX-12", ratchet: "0-80", bit: "GB" },
  "Silver Wolf":      { code: "UX-08", ratchet: "3-80", bit: "FB" },
  "Viper Tail":       { code: "BX-16", ratchet: "5-80", bit: "O" },
  "Wizard Arrow":     { code: "BX-03", ratchet: "4-80", bit: "B" },
  "Wizard Rod":       { code: "UX-03", ratchet: "5-70", bit: "DB" },
  "Wyvern Gale":      { code: "BX-24", ratchet: "5-80", bit: "GB" },
  "Phoenix Rudder":   { code: "UX-07", ratchet: "9-70", bit: "G" },
  "Crimson Garuda":   { code: "BX-38", ratchet: "4-70", bit: "TP" },
  "Hells Chain":      { code: "BX-21", ratchet: "5-60", bit: "HT" },
  "Hells Hammer":     { code: "UX-02", ratchet: "3-70", bit: "H" },
  "Hells Scythe":     { code: "BX-02", ratchet: "4-60", bit: "T" },
  "Leon Claw":        { code: "BX-15", ratchet: "5-60", bit: "P" },
  "Samurai Calibur":  { code: "BX-45", ratchet: "6-70", bit: "M" },
  "Scorpio Spear":    { code: "UX-14", ratchet: "0-70", bit: "Z" },
  "Unicorn Sting":    { code: "BX-26", ratchet: "5-60", bit: "GP" },
  "Weiss Tiger":      { code: "BX-33", ratchet: "3-60", bit: "U" },
  "Whale Wave":       { code: "BX-36", ratchet: "5-80", bit: "E" },
  "Dran Brave":       { code: "CX-01", ratchet: "6-60", bit: "V" },
  "Wizard Arc":       { code: "CX-02", ratchet: "4-55", bit: "LO" },
  "Perseus Dark":     { code: "CX-03", ratchet: "6-80", bit: "W" },
  "Hells Reaper":     { code: "CX-05", ratchet: "4-70", bit: "K" },
  "Fox Brush":        { code: "CX-06", ratchet: "9-70", bit: "GR" },
  "Pegasus Blast":    { code: "CX-07", ratchet: "INT", bit: "Tr" },
  "Cerberus Flame":   { code: "CX-08", ratchet: "5-80", bit: "WB" },
  "Sol Eclipse":      { code: "CX-09", ratchet: "5-70", bit: "TK" },
  "Wolf Hunt":        { code: "CX-10", ratchet: "0-60", bit: "DB" },
  "Emperor Might":    { code: "CX-11", ratchet: "INT", bit: "Op" },
  "Mammoth Tusk":     { code: "BX-00", ratchet: "2-80", bit: "E" },
  "Steel Samurai":    { code: "BX-00", ratchet: "5-70", bit: "GF", display: "Samurai Steel" },
  "Bahamut Blitz":    { code: "CX-13", ratchet: "1-50", bit: "I" },
  "Knight Fortress":  { code: "CX-14", ratchet: "8-70", bit: "UN" },
  "Ragna Rage":       { code: "CX-15", ratchet: "4-55", bit: "Y" },
  "Strike Dran":      { code: "BX-49", ratchet: "4-50", bit: "FF", display: "Dran Strike" },
  "Rocket Griffon":   { code: "UX-19", ratchet: "INT", bit: "H", display: "Bullet Griffon" },
  "Brachio Whip":     { code: "CX-18", ratchet: "5-70", bit: "Nr" },
  "Croco Crunch":     { code: "BX-00", ratchet: "2-60", bit: "Q", display: "Croc Crunch" },
  "Tyranno Roar":     { code: "UX-15", ratchet: "1-70", bit: "L" },
  "Heavens Ring":     { code: "BX-50", ratchet: "0-80", bit: "DS", aliases: ["Ring Aether"] },
  "Shelter Drake":    { code: "BX-39", ratchet: "7-80", bit: "GP" },
  "Ptera Swing":      { code: "UX-10", ratchet: "7-70", bit: "B" },
  "Unicorn Delta":    { code: "CX-17", ratchet: "3-60", bit: "GU", aliases: ["Delta Unicorn"] },
  "Glory Valkyrie":   { code: "UX-20", ratchet: "INT", bit: "LF" },
  "Bear Scratch":     { code: "BX-37", ratchet: "5-60", bit: "F" },
  "Shinobi Knife":    { code: "BX-00", ratchet: "4-60", bit: "LF" },
  "Goat Tackle":      { code: "BX-46", ratchet: "7-70", bit: "T" },
  "Shark Gill":       { code: "CX-11", ratchet: "5-60", bit: "FB" },
  "Phoenix Flare":    { code: "CX-12", ratchet: "9-80", bit: "WW" },
  "Leon Fang":        { code: "CX-00", ratchet: "4-60", bit: "A" },
};

// Entries still awaiting verification are listed here and skipped until filled.
const PENDING = new Set([]);

const spaced = (s) => s ? s.replace(/([a-z])([A-Z])/g, "$1 $2") : null;
const out = [];
const skipped = [];
for (const b of raw) {
  if (XOVER.has(b.name)) continue;
  const c = COMBOS[b.name];
  if (!c) { skipped.push(b.name); continue; }
  const bit = BITS[c.bit];
  if (!bit) throw new Error("unknown bit " + c.bit + " for " + b.name);
  const display = c.display || b.name;
  const id = display.toLowerCase().replace(/[^a-z0-9]+/g, "-");
  const m = META_OVERRIDES[id] ?? meta[id] ?? {};
  const year = m.year ?? (m.date ? Number(m.date.match(/\d{4}/)[0]) : null);
  const owner = "owner" in (META_OVERRIDES[id] ?? {}) ? META_OVERRIDES[id].owner : canonOwner(m.owner);
  if (!year) throw new Error("no release year for " + display);
  const aliases = new Set([b.name, spaced(b.tt), spaced(b.hasbro), ...(c.aliases || [])].filter(Boolean));
  aliases.delete(display);
  out.push({
    id,
    name: display,
    aliases: [...aliases],
    code: c.code,
    line: c.code.split("-")[0],
    type: b.type,
    spin: b.spin,
    weight: b.weight,
    atk: b.atk, def: b.def, sta: b.sta,
    ratchet: c.ratchet,
    bit: bit[0],
    bitAbbr: c.bit,
    bitType: bit[1],
    image: `img/blades/${imageFile(id)}`,
    year,
    owner,
  });
}
out.sort((a, b) => a.name.localeCompare(b.name));
// Rename any plainly named image left over from an earlier layout to its hashed name.
for (const b of out) {
  const plain = path.join(imgDir, b.id + ".webp");
  const hashed = path.join(root, "wwwroot", b.image);
  if (fs.existsSync(plain) && !fs.existsSync(hashed)) fs.renameSync(plain, hashed);
  if (!fs.existsSync(hashed)) console.warn("MISSING IMAGE for", b.name, "->", b.image);
}
fs.writeFileSync(outPath, JSON.stringify(out, null, 1) + "\n");
console.log(`wrote ${out.length} blades`);
const unexpected = skipped.filter((n) => !PENDING.has(n));
console.log("pending:", skipped.filter((n) => PENDING.has(n)).join(", "));
if (unexpected.length) console.log("UNEXPECTED SKIPS:", unexpected.join(", "));
