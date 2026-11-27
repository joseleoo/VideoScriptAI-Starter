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

function pickVoice(items: TtsVoice[]): TtsVoice | undefined {
  return items.find(isSpanish) ?? items[0];
}

export default function TextToSpeechPanel() {
  const [voices, setVoices] = useState<TtsVoice[]>([]);
  const [provider, setProvider] = useState<TtsProvider>("Windows");
  const [voiceId, setVoiceId] = useState("");
  const [text, setText] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [voiceError, setVoiceError] = useState<string | null>(null);
  const [audioUrl, setAudioUrl] = useState<string | null>(null);
  const [audioName, setAudioName] = useState("");

  const availableVoices = useMemo(
    () => voices
      .filter((voice) => voice.provider === provider)
      .sort((left, right) =>
        Number(isSpanish(right)) - Number(isSpanish(left)) ||
        left.locale.localeCompare(right.locale) ||
        left.name.localeCompare(right.name)),
    [voices, provider]
  );

  useEffect(() => {
    let active = true;

    listTtsVoices()
      .then((items) => {
        if (!active) return;
        setVoices(items);
        const preferredProvider: TtsProvider =
          items.some((item) => item.provider === "Windows" && isSpanish(item)) ? "Windows"
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
    setError(null);

    try {
      const result = await synthesizeSpeech({ text: text.trim(), provider, voiceId });
      setAudioName(result.fileName);
      setAudioUrl(URL.createObjectURL(result.blob));
    } catch (exception) {
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
            onChange={(event) => setProvider(event.target.value as TtsProvider)}
          >
            {providers.map((item) => (
              <option key={item.id} value={item.id}>{item.label}</option>
            ))}
          </select>
        </label>

        <label>
          Voz
          <select value={voiceId} onChange={(event) => setVoiceId(event.target.value)}>
            {availableVoices.length === 0 && <option value="">Sin voces disponibles</option>}
            {availableVoices.map((voice) => (
              <option key={voice.id} value={voice.id}>
                {voice.locale || "??"} · {voice.name} ({voice.gender})
              </option>
            ))}
          </select>
        </label>
      </div>

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

      {busy && (
        <div className="tts-progress" role="status" aria-live="polite">
          <span className="tts-hint">
            Generando MP3 con {provider === "Windows" ? "las voces de Windows" : "Edge TTS"}...
          </span>
          <div className="progress-track">
            <div className="progress-fill active indeterminate" />
          </div>
        </div>
      )}

      {voiceError && <div className="info-note">{voiceError}</div>}
      {voices.length > 0 && !voices.some(isSpanish) && (
        <div className="info-note">No se encontraron voces en español; se seleccionó la primera disponible.</div>
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
