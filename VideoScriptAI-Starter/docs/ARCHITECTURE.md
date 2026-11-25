# Architecture

## Style

Modular monolith with Clean Architecture boundaries.

- Domain: entities, enums and business invariants.
- Application: use cases and ports/interfaces.
- Infrastructure: EF Core, SQLite, filesystem, FFmpeg and whisper.cpp adapters.
- API: HTTP endpoints, dependency injection, SignalR and composition root.
- Frontend: React SPA communicating with the local API.

## Processing pipeline

1. Upload is validated and stored in an application-owned data directory.
2. A `TranscriptionJob` is persisted with Pending status.
3. Job ID is written to a bounded `Channel<Guid>`.
4. A hosted worker dequeues one job at a time.
5. FFmpeg extracts mono 16 kHz WAV audio.
6. whisper.cpp receives the audio and selected model.
7. JSON output is parsed into transcript segments.
8. Results are persisted and export files are generated.
9. SignalR broadcasts progress and final status.

## Resource policy

Initial concurrency is one job. The i5-12500H is a mobile 12-core CPU (4 performance + 8 efficiency cores); keep the first implementation conservative and benchmark before increasing thread count. Start whisper.cpp with 6 threads, then test 4, 6 and 8 on the same sample.

## Storage

Use `%LOCALAPPDATA%/VideoScriptAI` for database and application-managed files. Keep the original videos, extracted audio and exports in separate directories. Add retention/cleanup controls after the MVP.
