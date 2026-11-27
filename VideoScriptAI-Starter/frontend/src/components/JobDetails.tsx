import { useState } from "react";
import { deleteJob, downloadExport } from "../api";
import type { Job } from "../types";
import TranscriptEditor from "./TranscriptEditor";

type Props = {
  job: Job;
  onCancel: (id: string) => void;
  onTranscriptSaved: (job: Job) => void;
  onDeleted: (id: string) => void;
};

const activeStatuses = new Set(["Pending", "Validating", "ExtractingAudio", "Transcribing", "Exporting"]);

export default function JobDetails({ job, onCancel, onTranscriptSaved, onDeleted }: Props) {
  const [exportError, setExportError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const isActive = activeStatuses.has(job.status);

  async function exportFile(format: string) {
    setExportError(null);
    try {
      await downloadExport(job.id, format);
    } catch (exception) {
      setExportError(exception instanceof Error ? exception.message : "No se pudo exportar.");
    }
  }

  async function remove() {
    if (!window.confirm(`¿Eliminar el trabajo "${job.originalFileName}"? Esta acción no se puede deshacer.`))
      return;

    setBusy(true);
    setExportError(null);
    try {
      await deleteJob(job.id);
      onDeleted(job.id);
    } catch (exception) {
      setExportError(exception instanceof Error
        ? exception.message
        : "No se pudo eliminar el trabajo.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="panel details-panel">
      <div className="panel-header">
        <div>
          <h2 title={job.originalFileName}>{job.originalFileName}</h2>
          <p>
            {job.source === "Url" ? "URL" : "Archivo local"} · modelo {job.model.toLowerCase()} · idioma {job.detectedLanguage || job.language}
            {job.sourceUrl && (
              <> · <a className="source-link" href={job.sourceUrl} target="_blank" rel="noreferrer">Ver fuente</a></>
            )}
          </p>
        </div>
        <span className={`badge status-${job.status.toLowerCase()}`}>{job.status}</span>
      </div>

      {job.segments.length > 0 && (
        <div className="export-row">
          {["txt", "srt", "vtt", "json"].map((format) => (
            <button type="button" key={format} className="secondary" onClick={() => exportFile(format)}>
              Descargar {format.toUpperCase()}
            </button>
          ))}
        </div>
      )}
      {exportError && <div className="error-note">{exportError}</div>}

      <div className="progress-track large">
        <div className={`progress-fill ${isActive ? "active" : ""}`} style={{ width: `${job.progress}%` }} />
      </div>
      <div className="details-meta">
        <span>{job.progress}% completado</span>
        <span>Creado {new Date(job.createdAtUtc).toLocaleString()}</span>
      </div>

      {job.errorMessage && <div className="error-note">{job.errorMessage}</div>}

      <div className="tts-actions">
        {isActive && (
          <button type="button" className="secondary" onClick={() => onCancel(job.id)} disabled={busy}>
            Cancelar trabajo
          </button>
        )}
        <button type="button" className="danger" onClick={remove} disabled={busy}>
          {busy ? "Eliminando..." : "Eliminar trabajo"}
        </button>
      </div>

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
        </>
      )}
    </section>
  );
}
