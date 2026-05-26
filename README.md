# wsteam

`wsteam` is a cross-platform CLI tool for **installing Steam games using manifests** (instead of the standard Steam download flow).
It can search for a game by name, fetch the required manifests, download the depots, and place the game into your Steam library folder.

## Features

- Install games via manifests from:
  - **ManifestHub** (default)
  - **Hubcap**
- Search Steam apps by a query string (e.g. `"STEINS;GATE"`)
- Choose install location (defaults to your Steam `steamapps/common`)
- Filter by target OS (`--os`)
- **Exclude** specific depots from downloading (`--depots`)

## Requirements

- .NET (project targets `net10.0`)
- A valid API key for your selected manifest source

## Installation

### Build from source

```bash
dotnet build
```

### Publish (single-file, self-contained)

```bash
dotnet publish -c Release
```

### Arch Linux

This repo includes a `PKGBUILD` you can use to build a package locally:

```bash
makepkg -si
```

## Usage

### Install a game

```bash
wsteam install --source manifesthub --key <API_KEY> "STEINS;GATE"
```

### Use a custom install location

```bash
wsteam install --key <API_KEY> --location "/path/to/SteamLibrary/steamapps/common" "Game Name"
```

### Select manifest source

```bash
wsteam install --source hubcap --key <API_KEY> "Game Name"
```

### Filter OS

```bash
wsteam install --key <API_KEY> --os Windows "Game Name"
```

### Exclude specific depots

`--depots` is an exclusion list: depots you pass here will be skipped.

```bash
wsteam install --key <API_KEY> --depots 123456 234567 "Game Name"
```

## API keys

You must supply an API key via `--key`.

Notes:
- **ManifestHub** keys are expected to be **64 characters**.
- **Hubcap** keys are expected to start with **`smm`**.

The tool also sets environment variables for convenience:
- `MANIFEST_API_KEY` (ManifestHub)
- `HUBCAP_API_KEY` (Hubcap)

## How it finds your Steam directory

- **Linux**: checks common Steam locations:
  - `~/.steam/steam`
  - `~/.var/app/com.valvesoftware.Steam/data/Steam`
- **Windows**: reads `InstallPath` from the registry (`HKCU\Software\Valve\Steam`)

## Disclaimer

This project interacts with Steam content/manifests and third-party manifest sources. Use responsibly and only with content you are authorized to access.
