import * as SignalR from "@microsoft/signalr";
import type {
  CreateJobInput,
  Health,
  Job,
  JobProgressNotification,
  TranscriptSegment
} from "./types";

const API_BASE_URL = "http://localhost:5000";

async function readJson<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const detail = await response.text();
    let message = detail || `API request failed (${response.status})`;
    try {
      const parsed = JSON.parse(detail) as { message?: string };
      if (parsed.message) message = parsed.message;
    } catch { }
    throw new Error(message);
  }

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
