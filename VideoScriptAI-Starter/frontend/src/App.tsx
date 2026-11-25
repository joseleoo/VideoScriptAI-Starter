import { useCallback, useEffect, useState } from "react";
import type { HubConnection } from "@microsoft/signalr";
import { cancelJob, getHealth, getJob, listJobs, subscribeToJobProgress } from "./api";
import JobDetails from "./components/JobDetails";
import JobList from "./components/JobList";
import TextToSpeechPanel from "./components/TextToSpeechPanel";
import UploadPanel from "./components/UploadPanel";
import type { Health, Job } from "./types";

type AppMode = "video" | "speech";

export default function App() {
  const [mode, setMode] = useState<AppMode>("video");
  const [health, setHealth] = useState<Health | null>(null);
  const [jobs, setJobs] = useState<Job[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const replaceJob = useCallback((job: Job) => {
    setJobs((current) =>
      current.some((item) => item.id === job.id)
        ? current.map((item) => item.id === job.id ? job : item)
        : [job, ...current]);
  }, []);

  const refresh = useCallback(async () => {
    try {
      setJobs(await listJobs());
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "No se pudo cargar el historial.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    let active = true;
    let connection: HubConnection | null = null;

    getHealth()
      .then((result) => {
        if (active) setHealth(result);
      })
      .catch(() => {
        if (active) setError("No se pudo conectar con ASP.NET Core.");
      });

    void refresh();
    const interval = window.setInterval(() => void refresh(), 3000);

    subscribeToJobProgress((notification) => {
      setJobs((current) => current.map((job) =>
        job.id === notification.jobId
          ? { ...job, status: notification.status, progress: notification.progress }
          : job));

      if (notification.status === "Completed" || notification.status === "Failed" || notification.status === "Cancelled") {
        void getJob(notification.jobId)
          .then(replaceJob)
          .catch(() => undefined);
      }
    })
      .then((result) => {
        if (active) connection = result;
        else void result.stop();
      })
      .catch(() => {
        if (active) setError("SignalR no disponible; usando actualización periódica.");
      });

    return () => {
      active = false;
      window.clearInterval(interval);
      if (connection) void connection.stop();
    };
  }, [refresh, replaceJob]);

  function handleCreated(job: Job) {
    setJobs((current) => [job, ...current]);
    setSelectedId(job.id);
    void refresh();
  }

  async function handleCancel(id: string) {
    try {
      await cancelJob(id);
      await refresh();
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "No se pudo cancelar el trabajo.");
    }
  }

  const selectedJob = jobs.find((job) => job.id === selectedId) ?? null;

  return (
    <main className="shell">
      <header className="hero">
        <div className="eyebrow">LOCAL AI · VIDEO TRANSCRIPTION &amp; TTS</div>
        <h1>VideoScript <span>AI</span></h1>
        <p className="subtitle">
          {mode === "video"
            ? "Convierte tus videos en transcripciones, subtítulos y documentos. Todo se procesa localmente en tu equipo."
            : "Convierte texto en MP3 con voces locales de Windows o voces neurales de Edge TTS."}
        </p>
        <div className="status">
          <span className={`dot ${health ? "online" : ""}`} />
          {health
            ? `Backend conectado · motores ${health.enginesReady ? "listos" : "incompletos"}`
            : error ?? "Comprobando backend..."}
        </div>
        <nav className="mode-menu" aria-label="Modo de trabajo">
          <button
            type="button"
            className={mode === "video" ? "active" : ""}
            onClick={() => setMode("video")}
          >
            Video → TXT
          </button>
          <button
            type="button"
            className={mode === "speech" ? "active" : ""}
            onClick={() => setMode("speech")}
          >
            Texto → MP3
          </button>
        </nav>
      </header>

      {error && <div className="error-note global-error">{error}</div>}

      {mode === "video" ? (
        <div className="layout">
          <div className="layout-column">
            <UploadPanel onCreated={handleCreated} />
            <section className="panel history-panel">
              <div className="panel-header">
                <div>
                  <h2>Historial</h2>
                  <p>Trabajos de transcripción en este equipo.</p>
                </div>
              </div>
              {loading ? <div className="empty-note">Cargando...</div> : (
                <JobList jobs={jobs} selectedId={selectedId} onSelect={setSelectedId} />
              )}
            </section>
          </div>

          <div className="layout-column">
            {selectedJob ? (
              <JobDetails
                job={selectedJob}
                onCancel={(id) => void handleCancel(id)}
                onTranscriptSaved={replaceJob}
              />
            ) : (
              <section className="panel details-panel placeholder">
                <div className="upload-icon">◇</div>
                <h2>Sin trabajo seleccionado</h2>
                <p>Sube un video, pega una URL o elige un trabajo del historial para ver su progreso.</p>
              </section>
            )}
          </div>
        </div>
      ) : (
        <TextToSpeechPanel />
      )}

      <div className="footer-note">
        {mode === "video"
          ? "Procesamiento local · Sin cuotas por minuto · Sin envío a servicios externos"
          : "Windows: local · Edge TTS: envía el texto a Microsoft"}
      </div>
    </main>
  );
}
