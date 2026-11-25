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
