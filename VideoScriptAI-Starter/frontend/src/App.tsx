import { useEffect, useState } from "react";

type Health = {
  status: string;
  application: string;
  timestampUtc: string;
};

const API_BASE_URL = "http://localhost:5000";

export default function App() {
  const [health, setHealth] = useState<Health | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetch(`${API_BASE_URL}/api/health`)
      .then(async (response) => {
        if (!response.ok) throw new Error("API request failed");
        return response.json() as Promise<Health>;
      })
      .then(setHealth)
      .catch(() => setError("No se pudo conectar con ASP.NET Core."));
  }, []);

  return (
    <main className="shell">
      <section className="hero">
        <div className="eyebrow">LOCAL AI · VIDEO TRANSCRIPTION</div>
        <h1>VideoScript <span>AI</span></h1>
        <p className="subtitle">
          Convierte tus videos en transcripciones, subtítulos y documentos.
          Todo se procesa localmente en tu equipo.
        </p>
        <div className="status">
          <span className={`dot ${health ? "online" : ""}`} />
          {health
            ? `Backend conectado · ${health.application}`
            : error ?? "Comprobando backend..."}
        </div>
      </section>

      <section className="panel">
        <div>
          <h2>Nuevo proyecto</h2>
          <p>La siguiente entrega incorporará la carga de videos y la configuración de transcripción.</p>
        </div>
        <div className="dropzone">
          <div className="upload-icon">↑</div>
          <strong>Zona de carga de videos</strong>
          <span>MP4, MKV, MOV y AVI · Próxima implementación</span>
        </div>
        <div className="footer-note">Procesamiento local · Sin cuotas por minuto · Sin envío a servicios externos</div>
      </section>
    </main>
  );
}
