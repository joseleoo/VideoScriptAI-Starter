import { useState } from "react";
import { downloadExport } from "../api";
import type { Job } from "../types";
import TranscriptEditor from "./TranscriptEditor";

type Props = {
  job: Job;
  onCancel: (id: string) => void;
  onTranscriptSaved: (job: Job) => void;
};

const activeStatuses = new Set(["Pending", "Validating", "ExtractingAudio", "Transcribing", "Exporting"]);

export default function JobDetails({ job, onCancel, onTranscriptSaved }: Props) {
  const [exportError, setExportError] = useState<string | null>(null);
  const isActive = activeStatuses.has(job.status);

  async function exportFile(format: string) {
    setExportError(null);
    try {
      await downloadExport(job.id, format);
    } catch (exception) {
      setExportError(exception instanceof Error ? exception.message : "No se pudo exportar.");
    }
  }

  return (
    <section className="panel details-panel">
      <div className="panel-header">
        <div>
          <h2 title={job.originalFileName}>{job.originalFileName}</h2>
          <p>
            Modelo {job.model.toLowerCase()} · idioma {job.detectedLanguage || job.language}
          </p>
        </div>
        <span className={`badge status-${job.status.toLowerCase()}`}>{job.status}</span>
      </div>

      <div className="progress-track large">
        <div className={`progress-fill ${isActive ? "active" : ""}`} style={{ width: `${job.progress}%` }} />
      </div>
      <div className="details-meta">
        <span>{job.progress}% completado</span>
        <span>Creado {new Date(job.createdAtUtc).toLocaleString()}</span>
      </div>

      {job.errorMessage && <div className="error-note">{job.errorMessage}</div>}

      {isActive && (
        <button type="button" className="secondary" onClick={() => onCancel(job.id)}>
          Cancelar trabajo
        </button>
      )}

      {job.segments.length > 0 && (
        <>
          <TranscriptEditor job={job} onSaved={onTranscriptSaved} />
          <div className="export-row">
            {["txt", "srt", "vtt", "json"].map((format) => (
              <button type="button" key={format} className="secondary" onClick={() => exportFile(format)}>
                Descargar {format.toUpperCase()}
              </button>
            ))}
          </div>
          {exportError && <div className="error-note">{exportError}</div>}
        </>
      )}
    </section>
  );
}
