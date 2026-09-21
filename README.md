# Beydle

A daily Wordle-style guessing game for Beyblade X blades, built with Blazor WebAssembly and hosted as a static site on GitHub Pages.

One blade is chosen per day (midnight Europe/Berlin). Every guess shows how it compares with the hidden blade across type, spin direction, blade weight, attack / defense / stamina ratings, the stock ratchet and bit, release year and anime owner. Unlimited guesses. Solving it unlocks round 2: a second, different blade shown as a silhouette that sharpens with every wrong guess. Stats and today's guesses are kept in the browser's local storage. Twelve hidden achievements (see `Services/Achievements.cs`) pop up Steam-style when found and are listed in the statistics dialog. A practice mode plays both rounds with random blades without touching the daily stats.

## Run locally

```bash
dotnet run
```

Then open http://localhost:5181.

## Tests

```bash
dotnet test
```

## Deploy

Pushing to `main` runs `.github/workflows/deploy.yml`, which publishes the app and deploys it to GitHub Pages at `beydle.com`.

One-time setup in the GitHub repo: **Settings → Pages → Build and deployment → Source: GitHub Actions**.

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

## Cheating

This is a static site, so the answer is always discoverable by someone who opens the browser developer tools; that is true of every static Wordle clone. What is done: the schedule stores hashes rather than blade ids, image file names are hashed, all renders are prefetched together when round 2 starts so the network log does not single one out, and the blade id never appears in the page during round 2. No plain-text answer exists in the download; reading one requires reverse-engineering the app. Anything stronger needs a small server that holds the day's answers and evaluates guesses.

<a href="https://www.buymeacoffee.com/DawnDevelop"><img src="https://img.buymeacoffee.com/button-api/?text=Buy me a coffee&emoji=&slug=DawnDevelop&button_colour=5F7FFF&font_colour=ffffff&font_family=Cookie&outline_colour=000000&coffee_colour=FFDD00" /></a>