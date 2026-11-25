import { useEffect, useState } from "react";
import { updateTranscript } from "../api";
import type { Job, TranscriptSegment } from "../types";

type Props = {
  job: Job;
  onSaved: (job: Job) => void;
};

function formatTime(seconds: number): string {
  const totalMilliseconds = Math.round(seconds * 1000);
  const minutes = Math.floor(totalMilliseconds / 60000);
  const remainder = totalMilliseconds % 60000;
  const secs = Math.floor(remainder / 1000);
  const ms = remainder % 1000;
  return `${String(minutes).padStart(2, "0")}:${String(secs).padStart(2, "0")}.${String(ms).padStart(3, "0")}`;
}

export default function TranscriptEditor({ job, onSaved }: Props) {
  const signature = JSON.stringify(job.segments);
  const [segments, setSegments] = useState<TranscriptSegment[]>(job.segments);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    setSegments(job.segments);
  }, [job.id, signature]);

  function changeText(index: number, text: string) {
    setSegments((current) =>
      current.map((segment, position) => position === index ? { ...segment, text } : segment));
  }

  async function save() {
    setSaving(true);
    setMessage(null);

    try {
      const updated = await updateTranscript(job.id, segments);
      onSaved(updated);
      setMessage("Transcripción guardada.");
    } catch (exception) {
      setMessage(exception instanceof Error ? exception.message : "No se pudo guardar.");
    } finally {
      setSaving(false);
    }
  }

  if (segments.length === 0)
    return <div className="empty-note">La transcripción aparecerá cuando el trabajo termine.</div>;

  return (
    <div className="transcript-editor">
      <div className="editor-header">
        <h3>Transcripción</h3>
        <button type="button" onClick={save} disabled={saving}>
          {saving ? "Guardando..." : "Guardar cambios"}
        </button>
      </div>

      {segments.map((segment, index) => (
        <div className="segment" key={index}>
          <span className="timestamp">
            {formatTime(segment.startSeconds)} – {formatTime(segment.endSeconds)}
          </span>
          <textarea
            value={segment.text}
            rows={2}
            onChange={(event) => changeText(index, event.target.value)}
          />
        </div>
      ))}

      {message && <div className="info-note">{message}</div>}
    </div>
  );
}
