export type TranscriptionStatus =
  | "Pending"
  | "Validating"
  | "ExtractingAudio"
  | "Transcribing"
  | "Exporting"
  | "Completed"
  | "Failed"
  | "Cancelled";

export type TranscriptionModel = "Base" | "Small" | "Medium";
export type JobSource = "Upload" | "Url";

export type Health = {
  status: string;
  application: string;
  enginesReady: boolean;
  recordingAvailable: boolean;
  timestampUtc: string;
};

export type TranscriptSegment = {
  startSeconds: number;
  endSeconds: number;
  text: string;
};

export type Job = {
  id: string;
  originalFileName: string;
  source: JobSource;
  sourceUrl: string | null;
  language: string;
  detectedLanguage: string;
  model: TranscriptionModel;
  status: TranscriptionStatus;
  progress: number;
  errorMessage: string | null;
  createdAtUtc: string;
  completedAtUtc: string | null;
  fullText: string;
  segments: TranscriptSegment[];
};

export type JobProgressNotification = {
  jobId: string;
  status: TranscriptionStatus;
  progress: number;
  message: string | null;
};

export type CreateJobInput =
  | { kind: "file"; file: File; language: string; model: string }
  | { kind: "url"; url: string; language: string; model: string };

export type TtsProvider = "Windows" | "Edge";

export type TtsVoice = {
  provider: TtsProvider;
  id: string;
  name: string;
  locale: string;
  gender: string;
};

export type SynthesizeSpeechInput = {
  text: string;
  provider: TtsProvider;
  voiceId: string;
};

export type MeetingStatus =
  | "Pending"
  | "Recording"
  | "Validating"
  | "Transcribing"
  | "Summarizing"
  | "Completed"
  | "Failed"
  | "Cancelled";

export type MeetingSource = "Upload" | "Live";
export type SummaryProvider = "Ollama" | "OpenAI" | "Extractive";

export type MeetingSegment = {
  position: number;
  startSeconds: number;
  endSeconds: number;
  text: string;
  speaker: string;
};

export type Meeting = {
  id: string;
  title: string;
  source: MeetingSource;
  language: string;
  detectedLanguage: string;
  summaryProvider: SummaryProvider;
  status: MeetingStatus;
  progress: number;
  errorMessage: string | null;
  createdAtUtc: string;
  completedAtUtc: string | null;
  fullText: string;
  summary: string;
  segments: MeetingSegment[];
};

export type MeetingProgressNotification = {
  meetingId: string;
  status: MeetingStatus;
  progress: number;
  message: string | null;
};

export type RecordingStatus = {
  isRecording: boolean;
  meetingId: string | null;
  startedAtUtc: string | null;
  bytesWritten: number;
};

export type SummaryProviderInfo = {
  provider: SummaryProvider;
  label: string;
  available: boolean;
  isDefault: boolean;
};
