import { useState } from "react";
import { createJob } from "../api";
import type { Job } from "../types";

type Props = {
  onCreated: (job: Job) => void;
};

export default function UploadPanel({ onCreated }: Props) {
  const [file, setFile] = useState<File | null>(null);
  const [language, setLanguage] = useState("auto");
  const [model, setModel] = useState("small");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [dragging, setDragging] = useState(false);

  async function submit() {
    if (!file) {
      setError("Selecciona un video para continuar.");
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      const job = await createJob({ file, language, model });
      onCreated(job);
      setFile(null);
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
          <p>Sube un video y procesa la transcripción en tu equipo.</p>
        </div>
        <span className="pill">Local</span>
      </div>

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
          {submitting ? "Creando..." : "Transcribir video"}
        </button>
      </div>

      {error && <div className="error-note">{error}</div>}
    </section>
  );
}
