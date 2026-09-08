# wsteam

Cross-platform CLI that installs Steam games from depot manifests and downloads
Workshop items (including collections) into your Steam library — instead of using
the standard Steam download flow.

## Features

- Manifest-driven installs with built-in source adapters (`manifesthub` default, `hubcap`)
- Search apps by query, filter by OS (`--os`), exclude depots (`--depots`), choose install location
- Workshop downloads: `wsteam workshop <appId> <pubFileId>`, collections expanded recursively
- Resume support: existing chunks are verified (Adler32), only missing/invalid ones re-download
- Parallel chunk pipeline: ~70 MB/s on gigabit vs 2–3 MB/s single-threaded (~25×)
- `ArrayPool<byte>` rented buffers keep GC pressure flat; CDN rotation with per-server failure tracking
- Live progress UI with speed and ETA

## Requirements

- .NET (`net10.0`), a manifest source API key, and a Steam session for session-dependent flows (Workshop)

## Installation

```bash
dotnet build                # build
dotnet publish -c Release   # single-file, self-contained
makepkg -si                 # Arch Linux (PKGBUILD included)
```

## Usage

```bash
wsteam install --key <API_KEY> "STEINS;GATE"                 # search & install
wsteam install --source hubcap --key <API_KEY> "Game Name"   # pick manifest source
wsteam install --key <API_KEY> --os Windows "Game Name"      # filter OS
wsteam install --key <API_KEY> --depots 123456 234567 "Game" # skip depots
wsteam install --key <API_KEY> --location /path/to/common "Game Name"
wsteam workshop 4000 1234567890                              # workshop item/collection
```

Workshop output lands in `<steam>/steamapps/workshop/content/<appId>/<pubFileId>/`.

## Environment

- `MANIFEST_API_KEY` / `HUBCAP_API_KEY`: manifest source keys (alternative to `--key`)
- `PARALLELISM`: chunk download parallelism, 1–256 (default: cores + 4, clamped 4–32)

## How it works

1. Search app and depots via PICS, filter by OS, language, exclusions
2. Fetch manifests from the selected source, decrypt filenames with the depot key
3. Flatten manifest into chunk tasks, allocate files, verify existing chunks (Adler32)
4. Download missing chunks in parallel from Steam CDN servers, decrypt, write at offset
5. Report overall progress with speed and ETA

## Steam directory detection

- Linux: `~/.steam/steam`, `~/.var/app/com.valvesoftware.Steam/data/Steam`
- Windows: registry `HKCU\Software\Valve\Steam` (`InstallPath`)

## Roadmap

- Pluggable manifest endpoints (bring your own source)
- Manifest caching

## Legal

Intended for content **you own or are authorized to access**. Use only manifest sources
and depot keys you are authorized to use, third-party archives may conflict with the
Steam Subscriber Agreement.
Not affiliated with or endorsed by Valve.
