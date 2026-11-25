# VideoScript AI

Local-first video transcription platform built with ASP.NET Core 10, React, TypeScript, SQLite, FFmpeg and whisper.cpp.

## Current starter scope

This repository contains the initial solution structure and architectural contracts. The transcription engine is intentionally an adapter boundary: the next implementation step is to connect the `whisper-cli` executable and FFmpeg.

## Requirements

- Windows 10/11 x64
- .NET 10 SDK (a newer SDK can also target `net10.0`; the 10.x runtime is required)
- Node.js LTS (for Vite development/build only)
- FFmpeg
- whisper.cpp Windows CPU build (`whisper-cli.exe`)
- 32 GB RAM recommended for the planned Small/Medium model options

## Repository layout

- `backend/`: .NET solution and projects
- `frontend/`: React + TypeScript + Vite
- `engines/`: place external FFmpeg/Whisper binaries and models here (do not commit binaries/models)
- `storage/`: optional scratch folder (ignored by Git); runtime data defaults to `%LOCALAPPDATA%/VideoScriptAI`
- `docs/`: architecture and implementation notes

## First-time setup

1. Install .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0
2. Install Node.js LTS: https://nodejs.org/
3. Install FFmpeg and ensure `ffmpeg -version` works in PowerShell.
4. Download/build whisper.cpp for Windows CPU from https://github.com/ggml-org/whisper.cpp.
5. Put `whisper-cli.exe` under `engines/whisper/`.
6. Download a GGML model (start with `ggml-small.bin`) into `engines/whisper/models/`.
7. Open a terminal in `backend/` and run:
   ```powershell
   dotnet restore
   dotnet build
   dotnet run --project src/VideoScriptAI.API
   ```
8. Open another terminal in `frontend/` and run:
   ```powershell
   npm install
   npm run dev
   ```
9. Visit the Vite URL shown in the terminal (normally http://localhost:5173).

## Initial configuration

Set these environment variables before running the API, or later move them into appsettings:
- `VIDEOSCRIPTAI_FFMPEG_PATH` - full path to `ffmpeg.exe`
- `VIDEOSCRIPTAI_WHISPER_PATH` - full path to `whisper-cli.exe`
- `VIDEOSCRIPTAI_MODELS_PATH` - folder containing GGML model files
- `VIDEOSCRIPTAI_STORAGE_PATH` - local data folder

Example PowerShell:
```powershell
$env:VIDEOSCRIPTAI_FFMPEG_PATH = "C:\tools\ffmpeg\bin\ffmpeg.exe"
$env:VIDEOSCRIPTAI_WHISPER_PATH = "$PWD\engines\whisper\whisper-cli.exe"
$env:VIDEOSCRIPTAI_MODELS_PATH = "$PWD\engines\whisper\models"
$env:VIDEOSCRIPTAI_STORAGE_PATH = "$PWD\storage"
```

## Important engineering decisions

- CPU-only for MVP. Do not expose GPU selection until a compatible backend is detected and tested.
- One active transcription at a time initially.
- `Channel<T>` + `BackgroundService` for background work; never hold an HTTP request open during inference.
- SignalR will publish job status/progress.
- SQLite stores metadata and transcript segments. Video/audio/model binaries remain on disk.
- Never concatenate user-controlled strings into shell commands. Use `ProcessStartInfo.ArgumentList`.
- Validate file size, extension, content and storage paths. Keep uploads outside the web root.
- Do not commit user videos, generated transcripts, downloaded models or executables.

## Implementation milestones

1. Make the API build and run.
2. Add EF Core SQLite persistence and migrations.
3. Implement upload and `TranscriptionJob` creation.
4. Implement FFmpeg audio extraction adapter.
5. Implement whisper.cpp process adapter and parse JSON output.
6. Add queue worker and cancellation.
7. Add SignalR progress events.
8. Build React upload/configuration/history screens.
9. Add transcript editor and TXT/SRT/VTT/JSON exports.
10. Add automated tests and GitHub Actions.

See `docs/IMPLEMENTATION.md` and `docs/ARCHITECTURE.md`.
