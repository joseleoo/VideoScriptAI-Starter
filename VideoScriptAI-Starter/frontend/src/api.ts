import * as SignalR from "@microsoft/signalr";
import type {
  CreateJobInput,
  Health,
  Job,
  JobProgressNotification,
  Meeting,
  MeetingProgressNotification,
  MeetingSegment,
  RecordingStatus,
  SynthesizeSpeechInput,
  SummaryProvider,
  SummaryProviderInfo,
  TranscriptSegment,
  TtsVoice
} from "./types";

const API_BASE_URL = "http://localhost:5000";

async function readErrorMessage(response: Response): Promise<string> {
  const detail = await response.text();
  let message = detail || `API request failed (${response.status})`;
  try {
    const parsed = JSON.parse(detail) as { message?: string };
    if (parsed.message) message = parsed.message;
  } catch { }

  return message;
}

async function readJson<T>(response: Response): Promise<T> {
  if (!response.ok)
    throw new Error(await readErrorMessage(response));

  return (await response.json()) as T;
}

export async function getHealth(): Promise<Health> {
  const response = await fetch(`${API_BASE_URL}/api/health`);
  return readJson<Health>(response);
}

export async function listJobs(): Promise<Job[]> {
  const response = await fetch(`${API_BASE_URL}/api/jobs`);
  return readJson<Job[]>(response);
}

export async function getJob(id: string): Promise<Job> {
  const response = await fetch(`${API_BASE_URL}/api/jobs/${id}`);
  return readJson<Job>(response);
}

export async function createJob(input: CreateJobInput): Promise<Job> {
  const response = input.kind === "file"
    ? await createFromFile(input)
    : await createFromUrl(input);

  return readJson<Job>(response);
}

async function createFromFile(input: Extract<CreateJobInput, { kind: "file" }>): Promise<Response> {
  const form = new FormData();
  form.append("file", input.file);
  form.append("language", input.language);
  form.append("model", input.model);

  return fetch(`${API_BASE_URL}/api/jobs`, {
    method: "POST",
    body: form
  });
}

async function createFromUrl(input: Extract<CreateJobInput, { kind: "url" }>): Promise<Response> {
  return fetch(`${API_BASE_URL}/api/jobs/from-url`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      url: input.url,
      language: input.language,
      model: input.model
    })
  });
}

export async function cancelJob(id: string): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/jobs/${id}/cancel`, {
    method: "POST"
  });

  if (!response.ok)
    throw new Error(await response.text());
}

export async function deleteJob(id: string): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/jobs/${id}`, {
    method: "DELETE"
  });
  if (!response.ok)
    throw new Error(await readErrorMessage(response));
}

export async function updateTranscript(
  id: string,
  segments: TranscriptSegment[]
): Promise<Job> {
  const response = await fetch(`${API_BASE_URL}/api/jobs/${id}/transcript`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ segments })
  });

  return readJson<Job>(response);
}

export async function downloadExport(id: string, format: string): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/jobs/${id}/export/${format}`);
  if (!response.ok)
    throw new Error(await response.text());

  const blob = await response.blob();
  const disposition = response.headers.get("content-disposition");
  const match = disposition?.match(/filename="?([^";]+)"?/i);
  const fileName = match?.[1] ?? `transcript-${id}.${format}`;
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}

export async function listTtsVoices(): Promise<TtsVoice[]> {
  const response = await fetch(`${API_BASE_URL}/api/tts/voices`);
  return readJson<TtsVoice[]>(response);
}

export async function synthesizeSpeech(input: SynthesizeSpeechInput): Promise<{ blob: Blob; fileName: string }> {
  const response = await fetch(`${API_BASE_URL}/api/tts`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(input)
  });

  if (!response.ok)
    throw new Error(await readErrorMessage(response));

  const blob = await response.blob();
  const disposition = response.headers.get("content-disposition");
  const match = disposition?.match(/filename="?([^";]+)"?/i);
  return {
    blob,
    fileName: match?.[1] ?? "speech.mp3"
  };
}

export async function subscribeToJobProgress(
  onProgress: (notification: JobProgressNotification) => void
): Promise<SignalR.HubConnection> {
  const connection = new SignalR.HubConnectionBuilder()
    .withUrl(`${API_BASE_URL}/hubs/jobs`)
    .withAutomaticReconnect()
    .build();

  connection.on("jobProgress", onProgress);
  await connection.start();
  return connection;
}

export async function listMeetings(): Promise<Meeting[]> {
  const response = await fetch(`${API_BASE_URL}/api/meetings`);
  return readJson<Meeting[]>(response);
}

export async function getMeeting(id: string): Promise<Meeting> {
  const response = await fetch(`${API_BASE_URL}/api/meetings/${id}`);
  return readJson<Meeting>(response);
}

export async function listSummaryProviders(): Promise<SummaryProviderInfo[]> {
  const response = await fetch(`${API_BASE_URL}/api/meetings/providers`);
  return readJson<SummaryProviderInfo[]>(response);
}

export async function getRecordingStatus(): Promise<RecordingStatus> {
  const response = await fetch(`${API_BASE_URL}/api/meetings/recording`);
  return readJson<RecordingStatus>(response);
}

export async function createMeetingFromUpload(
  file: File,
  title: string,
  language: string,
  summaryProvider: SummaryProvider
): Promise<Meeting> {
  const form = new FormData();
  form.append("file", file);
  form.append("title", title);
  form.append("language", language);
  form.append("summaryProvider", summaryProvider);

  const response = await fetch(`${API_BASE_URL}/api/meetings`, {
    method: "POST",
    body: form
  });
  return readJson<Meeting>(response);
}

export async function startMeetingRecording(
  title: string,
  language: string,
  summaryProvider: SummaryProvider
): Promise<Meeting> {
  const response = await fetch(`${API_BASE_URL}/api/meetings/record/start`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ title, language, summaryProvider })
  });
  return readJson<Meeting>(response);
}

export async function stopMeetingRecording(): Promise<Meeting> {
  const response = await fetch(`${API_BASE_URL}/api/meetings/record/stop`, {
    method: "POST"
  });
  return readJson<Meeting>(response);
}

export async function cancelMeeting(id: string): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/meetings/${id}/cancel`, {
    method: "POST"
  });
  if (!response.ok)
    throw new Error(await readErrorMessage(response));
}

export async function deleteMeeting(id: string): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/meetings/${id}`, {
    method: "DELETE"
  });
  if (!response.ok)
    throw new Error(await readErrorMessage(response));
}

export async function updateMeetingSegments(
  id: string,
  segments: MeetingSegment[]
): Promise<Meeting> {
  const response = await fetch(`${API_BASE_URL}/api/meetings/${id}/segments`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ segments })
  });
  return readJson<Meeting>(response);
}

export async function updateMeetingSummary(id: string, summary: string): Promise<Meeting> {
  const response = await fetch(`${API_BASE_URL}/api/meetings/${id}/summary`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ summary })
  });
  return readJson<Meeting>(response);
}

export async function regenerateMeetingSummary(
  id: string,
  summaryProvider?: SummaryProvider
): Promise<Meeting> {
  const response = await fetch(`${API_BASE_URL}/api/meetings/${id}/summary/regenerate`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ summaryProvider: summaryProvider ?? null })
  });
  return readJson<Meeting>(response);
}

export async function retryMeetingConversion(id: string): Promise<Meeting> {
  const response = await fetch(`${API_BASE_URL}/api/meetings/${id}/retry`, {
    method: "POST"
  });
  return readJson<Meeting>(response);
}

export async function downloadMeetingExport(id: string, format: string): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/meetings/${id}/export/${format}`);
  if (!response.ok)
    throw new Error(await readErrorMessage(response));

  const blob = await response.blob();
  const disposition = response.headers.get("content-disposition");
  const match = disposition?.match(/filename="?([^";]+)"?/i);
  const fileName = match?.[1] ?? `meeting-notes-${id}.${format}`;
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}

export async function subscribeToMeetingProgress(
  onProgress: (notification: MeetingProgressNotification) => void
): Promise<SignalR.HubConnection> {
  const connection = new SignalR.HubConnectionBuilder()
    .withUrl(`${API_BASE_URL}/hubs/jobs`)
    .withAutomaticReconnect()
    .build();

  connection.on("meetingProgress", onProgress);
  await connection.start();
  return connection;
}
