# Beydle

A daily Wordle-style guessing game for Beyblade X blades, built with Blazor WebAssembly and hosted as a static site on Cloudflare Workers.

One blade is chosen per day (midnight Europe/Berlin). Every guess shows how it compares with the hidden blade across type, spin direction, blade weight, product line, the stock ratchet and bit, release year and anime owner. Unlimited guesses. A counter shows how many blades still fit every hint so far, and after the solve the page compares the player with Beydle Bot, a greedy solver (see `Services/Deduction.cs`). Xtreme mode only accepts guesses that fit all hints so far; it can be switched on before the first guess and off at any time. Solving it unlocks round 2: a second, different blade shown as a silhouette that sharpens with every wrong guess. Results can be shared as text or as an image. Stats and today's guesses are kept in the browser's local storage. Seventeen hidden achievements (see `Services/Achievements.cs`) pop up Steam-style when found and are listed in the statistics dialog. A practice mode plays both rounds with random blades without touching the daily stats.

`/meta` is a second page: a leaderboard of the blades, combos, ratchets, bits and three-blade decks that finish top 3 at World Beyblade Organization events (see [Meta leaderboard](#meta-leaderboard)).

## Run locally

```bash
dotnet run
``` 

Then open http://localhost:5181.

## Tests

```bash
dotnet test Beydle.sln
```

This runs the game's tests (`tests/Beydle.Tests`) and the meta indexer's (`meta/BeybladeMeta.Tests`).

## Deploy

Pushing to `main` triggers a Cloudflare Workers Build, which publishes the app and serves `output/wwwroot` as static assets at `beydle.com` (see `wrangler.jsonc`). `wwwroot/_headers` marks the fingerprinted `_framework` files as immutable.

Build settings in the Cloudflare dashboard (Worker → Settings → Build):

- Build command: `curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh && bash dotnet-install.sh --channel 10.0 --install-dir ./.dotnet && ./.dotnet/dotnet publish Beydle.csproj -c Release -o output`
- Deploy command: `npx wrangler deploy`

## Tooling

| Script | Purpose |
| --- | --- |
| `node tools/build-data.js` | Rebuilds `wwwroot/data/blades.json` from `tools/blades_raw.json` and `tools/blade_meta.json` (owner and release date reference data) plus the verified stock combos inside the script. Also renames blade images to their hashed file names. |
| `node tools/build-schedule.js` | Extends `wwwroot/data/schedule.json` from tomorrow onwards. Run it after adding a blade so the new blade enters the rotation without changing any day players may already be on. |

## Data

`wwwroot/data/blades.json` is the whole blade pool. Each entry:

| Field | Meaning |
| --- | --- |
| `name`, `aliases` | Takara Tomy name, plus Hasbro / alternative names accepted as guesses |
| `code`, `line` | Product code and line (BX / UX / CX) |
| `type`, `spin`, `weight` | Blade type, spin direction, blade-only weight in grams |
| `atk`, `def`, `sta` | 0–100 ratings |
| `ratchet` | Stock ratchet, or `INT` when the ratchet is integrated into the blade or bit |
| `bit`, `bitAbbr`, `bitType` | Stock bit, its abbreviation, and its type (used for the yellow "same type" hint) |
| `image` | Path under `wwwroot` to the blade render. File names are a salted hash of the id so the round-2 picture URL does not reveal the answer |
| `year` | First Takara Tomy release year |
| `released` | Full release date (`yyyy-MM-dd`) when known, otherwise `null`; drives the release-anniversary note |
| `owner` | Anime character who uses the blade (English dub spelling), or `null` |

## How the daily pick works

`wwwroot/data/schedule.json` maps every date to hashes of its two blade ids (`SHA-256(salt|date|id)`, see `ScheduleHash`) and is the source of truth; the app hashes each candidate for the day and takes the match. Days missing from the file fall back to the generator in `DailyPicker`, which is also what `tools/build-schedule.js` uses to fill the file: days are grouped into cycles of `pool size` days, each cycle a seeded shuffle of the whole pool, so every blade appears once per cycle and never on two consecutive days. The round-2 blade reads the same sequence from a fixed offset and skips the day's round-1 blade. There is no server: every visitor reads the same schedule. Because past and current days are kept when the schedule is regenerated, adding blades never changes a day that is already live.

## Stats

Every accepted guess sends one anonymous event to `POST /api/event`, handled by `worker/index.js`, which validates it and writes it to the Workers Analytics Engine dataset `beydle_events`. An event holds the date, mode, round, guessed blade id, guess number, whether it solved the round and whether Xtreme mode was on; no user id, cookie, IP address or local-storage data is sent or stored. The endpoint never returns data. The column layout is documented at the top of the worker.

Query it with the SQL API, using an API token with **Account Analytics: Read**:

```bash
curl "https://api.cloudflare.com/client/v4/accounts/$CLOUDFLARE_ACCOUNT_ID/analytics_engine/sql" \
  -H "Authorization: Bearer $CLOUDFLARE_API_TOKEN" \
  -d "SELECT blob3 AS blade, SUM(_sample_interval) AS guesses FROM beydle_events WHERE blob1 = 'daily' AND timestamp > NOW() - INTERVAL '7' DAY GROUP BY blade ORDER BY guesses DESC"
```

Other useful queries: daily players (`double1 = 1 AND blob2 = '1'`, grouped by `index1`), average guesses to solve (`AVG(double1)` where `blob4 = 'solved'`), and the most common first guess (`double1 = 1`, grouped by `blob3`).

## Meta leaderboard

`meta/` holds the indexer that feeds `/meta`, merged in from the former BeybladeMeta project. `BeybladeMeta.Core` parses posts of the WBO ["Winning Combinations" thread](https://worldbeyblade.org/Thread-Winning-Combinations-at-WBO-Organized-Events-Beyblade-X-BBX) into canonical combos, and `BeybladeMeta.Indexer` fetches new thread pages through ZenRows (which gets past the forum's Cloudflare check), stores them in SQLite and exports `wwwroot/data/meta/appearances.json`: one row per combo in a top-3 finish, with its placement, event date and a deck id grouping one player's three combos. No player names are stored. The page ranks that file in the browser (`Services/MetaLeaderboard.cs`, score 3/2/1 for 1st/2nd/3rd), and the Worker serves `/meta` with its own title and description.

`.github/workflows/meta-index.yml` runs the indexer every other day and commits the refreshed JSON, which triggers the Cloudflare deploy. It needs the repository secret `SCRAPER_API_KEY`. The SQLite database is rewritten on every run, so it is kept out of git as the asset `beyblade-meta.db` of the release `meta-db`, which the workflow downloads before and uploads after each run. Without it the indexer starts again from page 100 of the thread.

The indexer also writes `unmatched.json`: result lines inside a 1st/2nd/3rd block that did not match the parts vocabulary, usually a new part or a typo. It is excluded from the published site. To work on the parser, download the database into `meta/data/` (ignored by git) and run:

```bash
gh release download meta-db --pattern beyblade-meta.db --dir meta/data
# Re-export from the database without fetching:
EXPORT_ONLY=1 INDEXER_DB=meta/data/beyblade-meta.db INDEXER_OUT=wwwroot/data/meta dotnet run --project meta/BeybladeMeta.Indexer
# Re-parse the existing exports with the current parser, recovering unmatched lines:
REPROCESS=1 INDEXER_OUT=wwwroot/data/meta dotnet run --project meta/BeybladeMeta.Indexer
```

## Cheating

This is a static site, so the answer is always discoverable by someone who opens the browser developer tools; that is true of every static Wordle clone. What is done: the schedule stores hashes rather than blade ids, image file names are hashed, all renders are prefetched together when round 2 starts so the network log does not single one out, and the blade id never appears in the page during round 2. No plain-text answer exists in the download; reading one requires reverse-engineering the app. Anything stronger needs a small server that holds the day's answers and evaluates guesses.

<a href="https://www.buymeacoffee.com/DawnDevelop"><img src="https://img.buymeacoffee.com/button-api/?text=Buy me a coffee&emoji=&slug=DawnDevelop&button_colour=5F7FFF&font_colour=ffffff&font_family=Cookie&outline_colour=000000&coffee_colour=FFDD00" /></a>