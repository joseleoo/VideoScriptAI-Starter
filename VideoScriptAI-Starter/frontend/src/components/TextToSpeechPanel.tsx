import { useEffect, useMemo, useState } from "react";
import { listTtsVoices, synthesizeSpeech } from "../api";
import type { TtsProvider, TtsVoice } from "../types";

const providers: Array<{ id: TtsProvider; label: string }> = [
  { id: "Windows", label: "Windows · voces locales" },
  { id: "Edge", label: "Edge TTS · en línea" }
];

function isSpanish(voice: TtsVoice): boolean {
  return voice.locale.trim().toLowerCase().startsWith("es") ||
    voice.name.toLowerCase().includes("spanish") ||
    voice.name.toLowerCase().includes("español");
}

function isColombian(voice: TtsVoice): boolean {
  return voice.locale.trim().toLowerCase() === "es-co" ||
    voice.name.toLowerCase().startsWith("es-co-");
}

function pickVoice(items: TtsVoice[]): TtsVoice | undefined {
  return items.find(isColombian) ?? items.find(isSpanish) ?? items[0];
}

export default function TextToSpeechPanel() {
  const [voices, setVoices] = useState<TtsVoice[]>([]);
  const [voicesLoading, setVoicesLoading] = useState(true);
  const [provider, setProvider] = useState<TtsProvider>("Windows");
  const [voiceId, setVoiceId] = useState("");
  const [text, setText] = useState("");
  const [busy, setBusy] = useState(false);
  const [progress, setProgress] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [voiceError, setVoiceError] = useState<string | null>(null);
  const [audioUrl, setAudioUrl] = useState<string | null>(null);
  const [audioName, setAudioName] = useState("");

  const estimatedSeconds = useMemo(
    () => Math.min(120, Math.max(3, Math.round(text.trim().length / 250))),
    [text]
  );

  const availableVoices = useMemo(
    () => voices
      .filter((voice) => voice.provider === provider)
      .sort((left, right) =>
        Number(isColombian(right)) - Number(isColombian(left)) ||
        Number(isSpanish(right)) - Number(isSpanish(left)) ||
        left.locale.localeCompare(right.locale) ||
        left.name.localeCompare(right.name)),
    [voices, provider]
  );

  useEffect(() => {
    let active = true;
    setVoicesLoading(true);

    listTtsVoices()
      .then((items) => {
        if (!active) return;
        setVoices(items);
        const preferredProvider: TtsProvider =
          items.some((item) => item.provider === "Windows" && isColombian(item)) ? "Windows"
            : items.some((item) => item.provider === "Edge" && isColombian(item)) ? "Edge"
              : items.some((item) => item.provider === "Windows" && isSpanish(item)) ? "Windows"
                : items.some((item) => item.provider === "Edge" && isSpanish(item)) ? "Edge"
                  : "Windows";
        setProvider(preferredProvider);
        setVoiceId(pickVoice(items.filter((item) => item.provider === preferredProvider))?.id ?? "");
        if (!items.some((item) => item.provider === "Edge"))
          setVoiceError("Edge TTS no está disponible. Instala edge-tts para Python.");
      })
      .catch((exception) => {
        if (active)
          setVoiceError(exception instanceof Error
            ? exception.message
            : "No se pudieron cargar las voces.");
      })
      .finally(() => {
        if (active) setVoicesLoading(false);
      });

    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    if (availableVoices.length === 0) {
      setVoiceId("");
      return;
    }

    if (!availableVoices.some((voice) => voice.id === voiceId))
      setVoiceId(availableVoices[0].id);
  }, [availableVoices, voiceId]);

  useEffect(() => () => {
    if (audioUrl) URL.revokeObjectURL(audioUrl);
  }, [audioUrl]);

  useEffect(() => {
    if (!busy) return;
    const step = 100 / (estimatedSeconds * 10);
    const timer = window.setInterval(() => {
      setProgress((value) => Math.min(95, (value ?? 0) + step));
    }, 100);
    return () => window.clearInterval(timer);
  }, [busy, estimatedSeconds]);

  useEffect(() => {
    if (progress !== 100) return;
    const timer = window.setTimeout(() => setProgress(null), 1200);
    return () => window.clearTimeout(timer);
  }, [progress]);

  async function generate() {
    if (!text.trim()) {
      setError("Escribe el texto que quieres convertir.");
      return;
    }

    if (!voiceId) {
      setError("Selecciona una voz.");
      return;
    }

    setBusy(true);
    setProgress(0);
    setError(null);

    try {
      const result = await synthesizeSpeech({ text: text.trim(), provider, voiceId });
      setAudioName(result.fileName);
      setAudioUrl(URL.createObjectURL(result.blob));
      setProgress(100);
    } catch (exception) {
      setProgress(null);
      setError(exception instanceof Error
        ? exception.message
        : "No se pudo generar el audio.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="panel tts-panel">
      <div className="panel-header">
        <div>
          <h2>Texto a MP3</h2>
          <p>Convierte un texto en audio con voces de Windows o Edge TTS.</p>
        </div>
        <span className="pill">TTS</span>
      </div>

      <div className="tts-fields">
        <label>
          Proveedor de voz
          <select
            value={provider}
            disabled={voicesLoading || busy}
            onChange={(event) => setProvider(event.target.value as TtsProvider)}
          >
            {providers.map((item) => (
              <option key={item.id} value={item.id}>{item.label}</option>
            ))}
          </select>
        </label>

        <label>
          Voz
          <select
            value={voiceId}
            disabled={voicesLoading || busy}
            onChange={(event) => setVoiceId(event.target.value)}
          >
            {voicesLoading && <option value="">Cargando voces...</option>}
            {!voicesLoading && availableVoices.length === 0 && <option value="">Sin voces disponibles</option>}
            {availableVoices.map((voice) => (
              <option key={voice.id} value={voice.id}>
                {voice.locale || "??"} · {voice.name} ({voice.gender})
              </option>
            ))}
          </select>
        </label>
      </div>

      {voicesLoading && (
        <div className="voices-loading" role="status" aria-live="polite">
          <span className="spinner" aria-hidden="true" />
          <span>Cargando voces disponibles...</span>
        </div>
      )}

      <label className="tts-text-field">
        Texto
        <textarea
          rows={10}
          placeholder="Pega aquí el texto que quieres convertir en voz..."
          value={text}
          onChange={(event) => setText(event.target.value)}
        />
      </label>

      <div className="tts-actions">
        <button type="button" onClick={generate} disabled={busy || !voiceId}>
          {busy ? "Generando..." : "Generar MP3"}
        </button>
        <span className="tts-hint">
          {provider === "Windows"
            ? "Procesamiento local, sin envío a servicios externos."
            : "Edge TTS envía el texto a Microsoft y requiere internet."}
        </span>
      </div>

      {progress !== null && (
        <div className="tts-progress" role="status" aria-live="polite">
          <div className="tts-progress-label">
            <span className="tts-hint">
              {progress === 100
                ? "MP3 generado."
                : `Progreso estimado · ${provider === "Windows" ? "Windows" : "Edge TTS"}`}
            </span>
            <span className="tts-progress-value">{progress}%</span>
          </div>
          <div className="progress-track">
            <div className="progress-fill active" style={{ width: `${progress}%` }} />
          </div>
        </div>
      )}

      {voiceError && <div className="info-note">{voiceError}</div>}
      {voices.length > 0 && !voices.some(isColombian) && (
        <div className="info-note">
          {voices.some(isSpanish)
            ? "No se encontraron voces colombianas; se seleccionó una voz en español."
            : "No se encontraron voces en español; se seleccionó la primera disponible."}
        </div>
      )}
      {error && <div className="error-note">{error}</div>}

      {audioUrl && (
        <div className="audio-result">
          <h3>Resultado</h3>
          <audio controls src={audioUrl} />
          <a className="button-link" href={audioUrl} download={audioName}>Descargar MP3</a>
        </div>
      )}
    </section>
  );
}
