# Implementation guide

## Milestone 0 — Verify workstation

Run in PowerShell:

```powershell
dotnet --info
node --version
npm --version
ffmpeg -version
```

Check the actual integrated graphics adapter with Task Manager > Performance or Device Manager. GPU acceleration is not part of MVP regardless.

## Milestone 1 — Build the starter

```powershell
cd backend
dotnet restore
dotnet build
dotnet run --project src/VideoScriptAI.API
```

In another terminal:

```powershell
cd frontend
npm install
npm run dev
```

Open the Vite URL and verify the API health status. If the API uses a different port, update `API_BASE_URL` in `frontend/src/App.tsx` and the CORS origin in `Program.cs` as needed.

## Milestone 2 — SQLite

Add EF Core migrations after installing the EF CLI:

```powershell
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --project src/VideoScriptAI.Infrastructure --startup-project src/VideoScriptAI.API
dotnet ef database update --project src/VideoScriptAI.Infrastructure --startup-project src/VideoScriptAI.API
```

## Milestone 3 — FFmpeg adapter

Use `ProcessStartInfo` with `UseShellExecute = false`, redirect stdout/stderr, and populate `ArgumentList` one argument at a time. Extract mono 16 kHz PCM WAV. Await process exit asynchronously and kill the process tree on cancellation.

Never form a command string from an uploaded filename.

## Milestone 4 — whisper.cpp adapter

- Obtain a Windows CPU build of `whisper-cli.exe`.
- Download a compatible GGML model file.
- Start with the Small model.
- Invoke `whisper-cli` with model, audio path, language/auto-detection, thread count and JSON output options supported by the exact release you downloaded.
- Capture stdout/stderr and exit code.
- Parse JSON into `TranscriptionResult`.
- Test English and Spanish separately.
- Benchmark the same 3–5 minute video with Base, Small and Medium.

Do not hardcode CLI flags until verified against `whisper-cli.exe --help`; CLI options can evolve.

## Milestone 5 — Worker and progress

- Add `BackgroundService`.
- Dequeue job IDs from `IJobQueue`.
- Load the persisted job.
- Update state and progress after each stage.
- Publish updates through SignalR.
- Use one active job and a bounded queue.
- Persist failures and cancellation state.

## Milestone 6 — React workflow

Build in this order:
1. Upload screen and validation.
2. Language/model settings.
3. Job details and progress.
4. History.
5. Transcript viewer/editor.
6. Export buttons.

Use typed API service functions and a SignalR client. Keep the backend as the source of truth for job state.

## Milestone 7 — Exports

- TXT: full transcript.
- SRT: numbered segments with `HH:MM:SS,mmm` timestamps.
- VTT: `WEBVTT` header and `HH:MM:SS.mmm` timestamps.
- JSON: language, model metadata and timed segments.

Add unit tests for timestamp formatting, especially millisecond rounding and hour rollover.

## Definition of Done for MVP

- A local video can be uploaded.
- The API creates and persists a job.
- The job runs without blocking the HTTP request.
- Progress is visible in React.
- User can cancel a running job.
- Transcript can be viewed and edited.
- TXT, SRT, VTT and JSON can be downloaded.
- API and domain tests pass.
- No video, model or generated output is committed to Git.
