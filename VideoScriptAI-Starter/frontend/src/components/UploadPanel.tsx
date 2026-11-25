import { useState } from "react";
import { createJob } from "../api";
import type { Job } from "../types";

type Props = {
  onCreated: (job: Job) => void;
};

type SourceMode = "file" | "url";

export default function UploadPanel({ onCreated }: Props) {
  const [mode, setMode] = useState<SourceMode>("file");
  const [file, setFile] = useState<File | null>(null);
  const [url, setUrl] = useState("");
  const [language, setLanguage] = useState("auto");
  const [model, setModel] = useState("small");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [dragging, setDragging] = useState(false);

  function switchMode(next: SourceMode) {
    setMode(next);
    setError(null);
  }

  async function submit() {
    if (mode === "file" && !file) {
      setError("Selecciona un video para continuar.");
      return;
    }

    if (mode === "url" && !url.trim()) {
      setError("Pega una URL de video para continuar.");
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      const job = await createJob(mode === "file"
        ? { kind: "file", file: file!, language, model }
        : { kind: "url", url: url.trim(), language, model });

      onCreated(job);
      if (mode === "file") setFile(null);
      else setUrl("");
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "No se pudo crear el trabajo.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <section className="panel upload-panel">
      <div className="panel-header">
        <div>
          <h2>Nuevo proyecto</h2>
          <p>Sube un video o pega una URL y procesa la transcripción en tu equipo.</p>
        </div>
        <span className="pill">Local</span>
      </div>

      <div className="source-tabs" role="tablist">
        <button
          type="button"
          role="tab"
          aria-selected={mode === "file"}
          className={mode === "file" ? "active" : ""}
          onClick={() => switchMode("file")}
        >
          Archivo local
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={mode === "url"}
          className={mode === "url" ? "active" : ""}
          onClick={() => switchMode("url")}
        >
          URL de video
        </button>
      </div>

      {mode === "file" ? (
        <div
          className={`dropzone ${dragging ? "dragging" : ""}`}
          onDragOver={(event) => {
            event.preventDefault();
            setDragging(true);
          }}
          onDragLeave={() => setDragging(false)}
          onDrop={(event) => {
            event.preventDefault();
            setDragging(false);
            const dropped = event.dataTransfer.files[0];
            if (dropped) setFile(dropped);
          }}
        >
          <div className="upload-icon">↑</div>
          <strong>{file ? file.name : "Arrastra un video o haz clic para elegirlo"}</strong>
          <span>MP4, MKV, MOV y AVI</span>
          <input
            type="file"
            accept=".mp4,.mkv,.mov,.avi"
            onChange={(event) => setFile(event.target.files?.[0] ?? null)}
          />
        </div>
      ) : (
        <div className="url-field">
          <label htmlFor="video-url">URL directa del video</label>
          <input
            id="video-url"
            className="url-input"
            type="url"
            inputMode="url"
            placeholder="https://ejemplo.com/video.mp4"
            value={url}
            onChange={(event) => setUrl(event.target.value)}
          />
          <span>http:// o https:// · MP4, MKV, MOV o AVI</span>
          <div className="info-note">El video se descargará a este equipo antes de transcribirlo.</div>
        </div>
      )}

      <div className="settings-row">
        <label>
          Idioma
          <select value={language} onChange={(event) => setLanguage(event.target.value)}>
            <option value="auto">Detectar automáticamente</option>
            <option value="en">Inglés</option>
            <option value="es">Español</option>
            <option value="fr">Francés</option>
            <option value="de">Alemán</option>
            <option value="it">Italiano</option>
            <option value="pt">Portugués</option>
          </select>
        </label>
        <label>
          Modelo
          <select value={model} onChange={(event) => setModel(event.target.value)}>
            <option value="base">Base · rápido</option>
            <option value="small">Small · equilibrado</option>
            <option value="medium">Medium · preciso</option>
          </select>
        </label>
        <button type="button" onClick={submit} disabled={submitting}>
          {submitting
            ? "Creando..."
            : mode === "file" ? "Transcribir video" : "Transcribir desde URL"}
        </button>
      </div>

      {error && <div className="error-note">{error}</div>}
    </section>
  );
}
