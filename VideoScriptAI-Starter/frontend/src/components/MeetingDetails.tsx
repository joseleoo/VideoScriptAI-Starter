import { useEffect, useState } from "react";
import {
  cancelMeeting,
  deleteMeeting,
  downloadMeetingExport,
  regenerateMeetingSummary,
  updateMeetingSummary
} from "../api";
import type { Meeting, SummaryProvider, SummaryProviderInfo } from "../types";
import MeetingTranscriptEditor from "./MeetingTranscriptEditor";

type Props = {
  meeting: Meeting;
  providers: SummaryProviderInfo[];
  onUpdated: (meeting: Meeting) => void;
  onDeleted: (id: string) => void;
};

const activeStatuses = new Set(["Pending", "Recording", "Validating", "Transcribing", "Summarizing"]);

export default function MeetingDetails({ meeting, providers, onUpdated, onDeleted }: Props) {
  const [summary, setSummary] = useState(meeting.summary);
  const [regenerateProvider, setRegenerateProvider] = useState<SummaryProvider>(meeting.summaryProvider);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const isActive = activeStatuses.has(meeting.status);

  useEffect(() => {
    setSummary(meeting.summary);
  }, [meeting.id, meeting.summary]);

  useEffect(() => {
    setRegenerateProvider(meeting.summaryProvider);
  }, [meeting.id, meeting.summaryProvider]);

  async function saveSummary() {
    setBusy(true);
    setError(null);
    try {
      onUpdated(await updateMeetingSummary(meeting.id, summary));
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "No se pudo guardar el resumen.");
    } finally {
      setBusy(false);
    }
  }

  async function regenerate() {
    setBusy(true);
    setError(null);
    try {
      onUpdated(await regenerateMeetingSummary(meeting.id, regenerateProvider));
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "No se pudo generar el resumen.");
    } finally {
      setBusy(false);
    }
  }

  async function cancel() {
    setError(null);
    try {
      await cancelMeeting(meeting.id);
      onUpdated({ ...meeting, status: "Cancelled" });
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "No se pudo cancelar.");
    }
  }

  async function remove() {
    if (!window.confirm(`¿Eliminar la reunión "${meeting.title}"? Esta acción no se puede deshacer.`))
      return;

    setBusy(true);
    setError(null);
    try {
      await deleteMeeting(meeting.id);
      onDeleted(meeting.id);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "No se pudo eliminar la reunión.");
    } finally {
      setBusy(false);
    }
  }

  async function exportFile(format: string) {
    setError(null);
    try {
      await downloadMeetingExport(meeting.id, format);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "No se pudo exportar.");
    }
  }

  return (
    <section className="panel details-panel">
      <div className="panel-header">
        <div>
          <h2 title={meeting.title}>{meeting.title}</h2>
          <p>
            {meeting.source === "Live" ? "Grabación en vivo" : "Archivo subido"} · resumen {meeting.summaryProvider.toLowerCase()} · idioma {meeting.detectedLanguage || meeting.language}
          </p>
        </div>
        <span className={`badge status-${meeting.status.toLowerCase()}`}>{meeting.status}</span>
      </div>

      <div className="progress-track large">
        <div className={`progress-fill ${isActive ? "active" : ""}`} style={{ width: `${meeting.progress}%` }} />
      </div>
      <div className="details-meta">
        <span>{meeting.progress}% completado</span>
        <span>Creado {new Date(meeting.createdAtUtc).toLocaleString()}</span>
      </div>

      {meeting.errorMessage && <div className="error-note">{meeting.errorMessage}</div>}
      <div className="tts-actions">
        {isActive && (
          <button type="button" className="secondary" onClick={cancel}>
            Cancelar
          </button>
        )}
        <button type="button" className="danger" onClick={remove} disabled={busy}>
          {busy ? "Eliminando..." : "Eliminar reunión"}
        </button>
      </div>
      {error && <div className="error-note">{error}</div>}

      <div className="summary-section">
        <div className="editor-header">
          <h3>Resumen</h3>
          <div className="summary-actions">
            <select
              aria-label="Proveedor del resumen"
              value={regenerateProvider}
              onChange={event => setRegenerateProvider(event.target.value as SummaryProvider)}
              disabled={busy}
            >
              {providers.map(provider => (
                <option key={provider.provider} value={provider.provider} disabled={!provider.available}>
                  {provider.label}{provider.available ? "" : " (no configurado)"}
                </option>
              ))}
            </select>
            <button type="button" className="secondary" onClick={regenerate} disabled={busy || !meeting.fullText}>
              Regenerar
            </button>
          </div>
        </div>
        <textarea
          rows={9}
          aria-label="Resumen de la reunión"
          placeholder="El resumen aparecerá cuando termine el procesamiento. También puedes editarlo manualmente."
          value={summary}
          onChange={event => setSummary(event.target.value)}
        />
        <button type="button" onClick={saveSummary} disabled={busy}>
          {busy ? "Guardando..." : "Guardar resumen"}
        </button>
      </div>

      {meeting.segments.length > 0 && (
        <>
          <MeetingTranscriptEditor meeting={meeting} onSaved={onUpdated} />
          <div className="export-row">
            {["md", "txt", "json"].map(format => (
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
