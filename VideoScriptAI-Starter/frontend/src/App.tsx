import { useCallback, useEffect, useState } from "react";
import type { HubConnection } from "@microsoft/signalr";
import {
  cancelJob,
  getHealth,
  getJob,
  getRecordingStatus,
  listJobs,
  listMeetings,
  listSummaryProviders,
  listTtsVoices,
  subscribeToJobProgress
} from "./api";
import JobDetails from "./components/JobDetails";
import JobList from "./components/JobList";
import MeetingNotesPanel from "./components/MeetingNotesPanel";
import TextToSpeechPanel from "./components/TextToSpeechPanel";
import UploadPanel from "./components/UploadPanel";
import type { Health, Job } from "./types";

type AppMode = "video" | "speech" | "meetings";
type BootState = "pending" | "ready" | "failed";

const bootSteps = [
  { key: "api", label: "ASP.NET Core / API" },
  { key: "engines", label: "Motores locales (ffmpeg + whisper)" },
  { key: "signalr", label: "SignalR" },
  { key: "jobs", label: "Historial de trabajos" },
  { key: "meetings", label: "Notas de reunión" },
  { key: "speech", label: "Voces TTS" }
] as const;

type BootKey = typeof bootSteps[number]["key"];
type BootCheck = { key: BootKey; label: string; state: BootState };

export default function App() {
  const [mode, setMode] = useState<AppMode>("video");
  const [health, setHealth] = useState<Health | null>(null);
  const [jobs, setJobs] = useState<Job[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [healthError, setHealthError] = useState<string | null>(null);
  const [signalrError, setSignalrError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [booting, setBooting] = useState(true);
  const [bootChecks, setBootChecks] = useState<BootCheck[]>(() =>
    bootSteps.map((step) => ({ ...step, state: "pending" })));

  const markBoot = useCallback((key: BootKey, state: Exclude<BootState, "pending">) => {
    setBootChecks((current) => current.map((check) =>
      check.key === key ? { ...check, state } : check));
  }, []);

  const replaceJob = useCallback((job: Job) => {
    setJobs((current) =>
      current.some((item) => item.id === job.id)
        ? current.map((item) => item.id === job.id ? job : item)
        : [job, ...current]);
  }, []);

  const refresh = useCallback(async () => {
    try {
      setJobs(await listJobs());
      setHealthError(null);
      markBoot("jobs", "ready");
    } catch (exception) {
      setHealthError(exception instanceof Error ? exception.message : "No se pudo cargar el historial.");
      markBoot("jobs", "failed");
    } finally {
      setLoading(false);
    }
  }, [markBoot]);

  useEffect(() => {
    let active = true;
    let connection: HubConnection | null = null;

    setError(null);
    setHealthError(null);
    setSignalrError(null);
    setLoading(true);

    const checkHealth = async () => {
      try {
        const result = await getHealth();
        if (active) {
          setHealth(result);
          setHealthError(null);
          markBoot("api", "ready");
          markBoot("engines", result.enginesReady ? "ready" : "failed");
        }
      } catch {
        if (active) {
          setHealthError("No se pudo conectar con ASP.NET Core.");
          markBoot("api", "failed");
          markBoot("engines", "failed");
        }
      }
    };

    void checkHealth();
    const healthInterval = window.setInterval(() => void checkHealth(), 5000);

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
        if (active) {
          connection = result;
          markBoot("signalr", "ready");
        } else void result.stop();
      })
      .catch(() => {
        if (active) {
          setSignalrError("SignalR no disponible; usando actualización periódica.");
          markBoot("signalr", "failed");
        }
      });

    return () => {
      active = false;
      window.clearInterval(interval);
      window.clearInterval(healthInterval);
      if (connection) void connection.stop();
    };
  }, [markBoot, refresh, replaceJob]);

  useEffect(() => {
    let active = true;

    void (async () => {
      try {
        await Promise.all([listMeetings(), listSummaryProviders(), getRecordingStatus()]);
        if (active) markBoot("meetings", "ready");
      } catch {
        if (active) markBoot("meetings", "failed");
      }
    })();

    void (async () => {
      try {
        await listTtsVoices();
        if (active) markBoot("speech", "ready");
      } catch {
        if (active) markBoot("speech", "failed");
      }
    })();

    return () => {
      active = false;
    };
  }, [markBoot]);

  useEffect(() => {
    if (!booting || !bootChecks.every((check) => check.state !== "pending")) return;
    const timer = window.setTimeout(() => setBooting(false), 350);
    return () => window.clearTimeout(timer);
  }, [bootChecks, booting]);

  useEffect(() => {
    if (!booting) return;
    const timer = window.setTimeout(() => {
      setBootChecks((current) => current.map((check) =>
        check.state === "pending" ? { ...check, state: "failed" } : check));
      setBooting(false);
    }, 15000);
    return () => window.clearTimeout(timer);
  }, [booting]);

  function handleCreated(job: Job) {
    setJobs((current) => [job, ...current]);
    setSelectedId(job.id);
    void refresh();
  }

  function handleJobDeleted(id: string) {
    setJobs((current) => current.filter((job) => job.id !== id));
    setSelectedId((current) => current === id ? null : current);
  }

  async function handleCancel(id: string) {
    setError(null);
    try {
      await cancelJob(id);
      await refresh();
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "No se pudo cancelar el trabajo.");
    }
  }

  const selectedJob = jobs.find((job) => job.id === selectedId) ?? null;
  const notice = error ?? healthError ?? signalrError;

  return (
    <>
      {booting && (
        <div className="boot-overlay" role="status" aria-live="polite" aria-busy="true">
          <div className="boot-card">
            <span className="boot-spinner" aria-hidden="true" />
            <h2>Cargando dependencias</h2>
            <p>Preparando backend, SignalR y motores locales...</p>
            <ul className="boot-list">
              {bootChecks.map((check) => (
                <li key={check.key} className={`boot-item ${check.state}`}>
                  <span className="boot-mark" aria-hidden="true">
                    {check.state === "ready" ? "✓" : check.state === "failed" ? "!" : ""}
                  </span>
                  <span>{check.label}</span>
                  <span className="boot-state">
                    {check.state === "ready" ? "Listo" : check.state === "failed" ? "Error" : "Cargando"}
                  </span>
                </li>
              ))}
            </ul>
          </div>
        </div>
      )}

      <main className="shell" aria-hidden={booting}>
      <header className="hero">
        <div className="eyebrow">LOCAL AI · VIDEO TRANSCRIPTION &amp; TTS</div>
        <h1>VideoScript <span>AI</span></h1>
        <p className="subtitle">
          {mode === "video"
            ? "Convierte tus videos en transcripciones, subtítulos y documentos. Todo se procesa localmente en tu equipo."
            : mode === "speech"
              ? "Convierte texto en MP3 con voces locales de Windows o voces neurales de Edge TTS."
              : "Captura reuniones de Zoom, Meet o Teams: transcripción por hablantes y resumen automático."}
        </p>
        <div className="status">
          <span className={`dot ${health ? "online" : ""}`} />
          {health
            ? `Backend conectado · motores ${health.enginesReady ? "listos" : "incompletos"}`
            : healthError ?? "Comprobando backend..."}
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
          <button
            type="button"
            className={mode === "meetings" ? "active" : ""}
            onClick={() => setMode("meetings")}
          >
            Notas de reunión
          </button>
        </nav>
      </header>

      {notice && <div className="error-note global-error">{notice}</div>}

      {mode === "video" && (
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
                onDeleted={handleJobDeleted}
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
      )}

      <div hidden={mode !== "speech"}>
        <TextToSpeechPanel />
      </div>

      {mode === "meetings" && (
        <MeetingNotesPanel recordingAvailable={health?.recordingAvailable ?? false} />
      )}

      <div className="footer-note">
        {mode === "video"
          ? "Procesamiento local · Sin cuotas por minuto · Sin envío a servicios externos"
          : mode === "speech"
            ? "Windows: local · Edge TTS: envía el texto a Microsoft"
            : "Transcripción local · El resumen puede usar Ollama, OpenAI o un modo extractivo"}
      </div>
      </main>
    </>
  );
}
