import { useCallback, useEffect, useState } from "react";
import type { HubConnection } from "@microsoft/signalr";
import {
  cancelMeeting,
  createMeetingFromUpload,
  deleteMeeting,
  getMeeting,
  getRecordingStatus,
  listMeetings,
  listSummaryProviders,
  startMeetingRecording,
  stopMeetingRecording,
  subscribeToMeetingProgress
} from "../api";
import type {
  Meeting,
  RecordingCaptureSource,
  RecordingStatus,
  SummaryProvider,
  SummaryProviderInfo
} from "../types";
import MeetingDetails from "./MeetingDetails";

type Props = {
  recordingAvailable: boolean;
};

const languages = [
  { value: "auto", label: "Detectar idioma" },
  { value: "es", label: "Español" },
  { value: "en", label: "Inglés" },
  { value: "pt", label: "Portugués" },
  { value: "fr", label: "Francés" },
  { value: "de", label: "Alemán" },
  { value: "it", label: "Italiano" }
];

function formatElapsed(seconds: number): string {
  const minutes = Math.floor(seconds / 60);
  return `${String(minutes).padStart(2, "0")}:${String(seconds % 60).padStart(2, "0")}`;
}

export default function MeetingNotesPanel({ recordingAvailable }: Props) {
  const [sourceMode, setSourceMode] = useState<"live" | "upload">("live");
  const [meetings, setMeetings] = useState<Meeting[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [providers, setProviders] = useState<SummaryProviderInfo[]>([]);
  const [provider, setProvider] = useState<SummaryProvider>("Extractive");
  const [captureSource, setCaptureSource] = useState<RecordingCaptureSource>("Both");
  const [language, setLanguage] = useState("auto");
  const [title, setTitle] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [recording, setRecording] = useState<RecordingStatus>({
    isRecording: false,
    meetingId: null,
    startedAtUtc: null,
    bytesWritten: 0
  });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [, setTick] = useState(0);

  const replaceMeeting = useCallback((meeting: Meeting) => {
    setMeetings(current =>
      current.some(item => item.id === meeting.id)
        ? current.map(item => item.id === meeting.id ? meeting : item)
        : [meeting, ...current]);
  }, []);

  const load = useCallback(async () => {
    const [items, availableProviders, status] = await Promise.all([
      listMeetings(),
      listSummaryProviders(),
      getRecordingStatus()
    ]);

    setMeetings(items);
    setProviders(availableProviders);
    setRecording(status);
    const preferred = availableProviders.find(item => item.isDefault && item.available)
      ?? availableProviders.find(item => item.available);
    if (preferred) setProvider(preferred.provider);
  }, []);

  useEffect(() => {
    let active = true;
    let connection: HubConnection | null = null;

    load()
      .catch(exception => {
        if (active)
          setError(exception instanceof Error ? exception.message : "No se pudo conectar con el backend.");
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    const interval = window.setInterval(() => {
      void listMeetings().then(items => {
        if (active) setMeetings(items);
      }).catch(() => undefined);
      void getRecordingStatus().then(status => {
        if (active) setRecording(status);
      }).catch(() => undefined);
    }, 3000);

    subscribeToMeetingProgress(notification => {
      setMeetings(current => current.map(meeting =>
        meeting.id === notification.meetingId
          ? {
            ...meeting,
            status: notification.status,
            progress: notification.progress,
            errorMessage: notification.status === "Failed" ? notification.message : meeting.errorMessage
          }
          : meeting));

      if (notification.status === "Completed" || notification.status === "Failed" || notification.status === "Cancelled")
        void getMeeting(notification.meetingId).then(replaceMeeting).catch(() => undefined);
    })
      .then(result => {
        if (active) connection = result;
        else void result.stop();
      })
      .catch(() => undefined);

    return () => {
      active = false;
      window.clearInterval(interval);
      if (connection) void connection.stop();
    };
  }, [load, replaceMeeting]);

  useEffect(() => {
    if (!recording.isRecording) return;
    const interval = window.setInterval(() => setTick(value => value + 1), 1000);
    return () => window.clearInterval(interval);
  }, [recording.isRecording]);

  async function handleDelete(id: string) {
    setError(null);
    try {
      await deleteMeeting(id);
      setMeetings(current => current.filter(meeting => meeting.id !== id));
      if (selectedId === id) setSelectedId(null);
      setRecording(await getRecordingStatus());
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "No se pudo eliminar la reunión.");
    }
  }

  async function handleStart() {
    setBusy(true);
    setError(null);
    try {
      const meeting = await startMeetingRecording(title.trim(), language, provider, captureSource);
      replaceMeeting(meeting);
      setSelectedId(meeting.id);
      setRecording(await getRecordingStatus());
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "No se pudo iniciar la grabación.");
    } finally {
      setBusy(false);
    }
  }

  async function handleStop() {
    setBusy(true);
    setError(null);
    try {
      const meeting = await stopMeetingRecording();
      replaceMeeting(meeting);
      setSelectedId(meeting.id);
      setRecording(await getRecordingStatus());
      setTitle("");
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "No se pudo detener la grabación.");
    } finally {
      setBusy(false);
    }
  }

  async function handleDiscard() {
    if (!recording.meetingId) return;

    setBusy(true);
    setError(null);
    try {
      await cancelMeeting(recording.meetingId);
      setRecording(await getRecordingStatus());
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "No se pudo descartar la grabación.");
    } finally {
      setBusy(false);
    }
  }

  async function handleUpload() {
    if (!file) {
      setError("Selecciona un archivo de grabación.");
      return;
    }

    setBusy(true);
    setError(null);
    try {
      const meeting = await createMeetingFromUpload(file, title.trim(), language, provider);
      replaceMeeting(meeting);
      setSelectedId(meeting.id);
      setTitle("");
      setFile(null);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "No se pudo subir la grabación.");
    } finally {
      setBusy(false);
    }
  }

  const selectedMeeting = meetings.find(meeting => meeting.id === selectedId) ?? null;
  const elapsed = recording.startedAtUtc
    ? Math.max(0, Math.floor((Date.now() - Date.parse(recording.startedAtUtc)) / 1000))
    : 0;

  const providerSelect = (
    <label>
      Generar resumen con
      <select value={provider} onChange={event => setProvider(event.target.value as SummaryProvider)} disabled={busy}>
        {providers.length === 0 && <option value="Extractive">Extractivo</option>}
        {providers.map(item => (
          <option key={item.provider} value={item.provider} disabled={!item.available}>
            {item.label}{item.available ? "" : " (no configurado)"}
          </option>
        ))}
      </select>
    </label>
  );

  const languageSelect = (
    <label>
      Idioma
      <select value={language} onChange={event => setLanguage(event.target.value)} disabled={busy}>
        {languages.map(item => <option key={item.value} value={item.value}>{item.label}</option>)}
      </select>
    </label>
  );

  const captureSourceSelect = (
    <label>
      Fuente de audio
      <select
        value={captureSource}
        onChange={event => setCaptureSource(event.target.value as RecordingCaptureSource)}
        disabled={busy || recording.isRecording}
      >
        <option value="Both">Micrófono + audio del sistema</option>
        <option value="Microphone">Solo micrófono</option>
        <option value="System">Solo audio del sistema</option>
      </select>
    </label>
  );

  return (
    <div className="layout">
      <div className="layout-column">
        <section className="panel">
          <div className="panel-header">
            <div>
              <h2>Nuevas notas de reunión</h2>
              <p>Graba el audio de Zoom, Meet o Teams, o sube una grabación ya realizada.</p>
            </div>
            <span className="pill">MEETINGS</span>
          </div>

          <div className="source-tabs" role="tablist">
            <button
              type="button"
              role="tab"
              aria-selected={sourceMode === "live"}
              className={sourceMode === "live" ? "active" : ""}
              onClick={() => setSourceMode("live")}
            >
              Grabación en vivo
            </button>
            <button
              type="button"
              role="tab"
              aria-selected={sourceMode === "upload"}
              className={sourceMode === "upload" ? "active" : ""}
              onClick={() => setSourceMode("upload")}
            >
              Subir grabación
            </button>
          </div>

          <div className="settings-row">
            <label>
              Título
              <input
                className="text-input"
                placeholder="Ej. Daily del equipo"
                value={title}
                onChange={event => setTitle(event.target.value)}
                disabled={busy}
              />
            </label>
            {languageSelect}
            {providerSelect}
          </div>

          {sourceMode === "live" ? (
            <div className="meeting-capture">
              <div className="settings-row">{captureSourceSelect}</div>
              {recording.isRecording && (
                <div className="recording-badge">
                  <span className="recording-dot" /> Grabando {formatElapsed(elapsed)}
                </div>
              )}
              <div className="tts-actions">
                <button type="button" onClick={handleStart} disabled={busy || recording.isRecording}>
                  Iniciar grabación
                </button>
                <button type="button" className="secondary" onClick={handleStop} disabled={busy || !recording.isRecording}>
                  Detener y procesar
                </button>
                <button type="button" className="secondary" onClick={handleDiscard} disabled={busy || !recording.isRecording}>
                  Descartar grabación
                </button>
              </div>
              <div className="info-note">
                {!recordingAvailable && "El backend no ha confirmado la captura en Windows; si falla, usa la opción de subir una grabación. "}
                La opción recomendada mezcla tu micrófono con el audio del sistema para incluir a todos los participantes. Si no se capta sonido, comprueba el dispositivo seleccionado o sube la grabación.
              </div>
            </div>
          ) : (
            <div className="meeting-capture">
              <label className="dropzone meeting-dropzone">
                <span className="upload-icon">◇</span>
                <strong>{file ? file.name : "Selecciona o arrastra una grabación"}</strong>
                <span>WAV, MP3, M4A, OGG, WEBM, MP4, MOV o MKV</span>
                <input
                  type="file"
                  accept="audio/*,video/*"
                  onChange={event => setFile(event.target.files?.[0] ?? null)}
                />
              </label>
              <div className="tts-actions">
                <button type="button" onClick={handleUpload} disabled={busy || !file}>
                  Procesar grabación
                </button>
              </div>
            </div>
          )}

          {error && <div className="error-note">{error}</div>}
        </section>

        <section className="panel history-panel">
          <div className="panel-header">
            <div>
              <h2>Reuniones</h2>
              <p>Notas guardadas en este equipo.</p>
            </div>
          </div>
          {loading ? <div className="empty-note">Cargando...</div> : (
            <div className="job-list">
              {meetings.length === 0 && <div className="empty-note">Todavía no hay reuniones.</div>}
              {meetings.map(meeting => (
                <button
                  type="button"
                  key={meeting.id}
                  className={`job-row ${meeting.id === selectedId ? "selected" : ""}`}
                  onClick={() => setSelectedId(meeting.id)}
                >
                  <div className="job-row-top">
                    <strong>{meeting.title}</strong>
                    <span className={`badge status-${meeting.status.toLowerCase()}`}>{meeting.status}</span>
                  </div>
                  <div className="job-row-meta">
                    <span>{meeting.source === "Live" ? "En vivo" : "Subida"} · {meeting.summaryProvider}</span>
                    <span>{new Date(meeting.createdAtUtc).toLocaleString()}</span>
                  </div>
                </button>
              ))}
            </div>
          )}
        </section>
      </div>

      <div className="layout-column">
        {selectedMeeting ? (
          <MeetingDetails
            meeting={selectedMeeting}
            providers={providers}
            onUpdated={updated => {
              replaceMeeting(updated);
              setSelectedId(updated.id);
            }}
            onDeleted={id => void handleDelete(id)}
          />
        ) : (
          <section className="panel details-panel placeholder">
            <div className="upload-icon">◇</div>
            <h2>Sin reunión seleccionada</h2>
            <p>Inicia una grabación, sube una grabación o elige una reunión del historial.</p>
          </section>
        )}
      </div>
    </div>
  );
}
