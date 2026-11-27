import { useEffect, useMemo, useState } from "react";
import { updateMeetingSegments } from "../api";
import type { Meeting, MeetingSegment } from "../types";

type Props = {
  meeting: Meeting;
  onSaved: (meeting: Meeting) => void;
};

function formatTime(seconds: number): string {
  const total = Math.round(seconds);
  const minutes = Math.floor(total / 60);
  return `${String(minutes).padStart(2, "0")}:${String(total % 60).padStart(2, "0")}`;
}

export default function MeetingTranscriptEditor({ meeting, onSaved }: Props) {
  const signature = JSON.stringify(meeting.segments);
  const [segments, setSegments] = useState<MeetingSegment[]>(meeting.segments);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    setSegments(meeting.segments);
  }, [meeting.id, signature]);

  const speakers = useMemo(
    () => Array.from(new Set(segments.map(segment => segment.speaker.trim()).filter(Boolean))).sort(),
    [segments]
  );

  function change(index: number, patch: Partial<MeetingSegment>) {
    setSegments(current =>
      current.map((segment, position) => position === index ? { ...segment, ...patch } : segment));
  }

  async function save() {
    setSaving(true);
    setMessage(null);
    try {
      const updated = await updateMeetingSegments(meeting.id, segments);
      onSaved(updated);
      setMessage("Hablantes y transcripción guardados.");
    } catch (exception) {
      setMessage(exception instanceof Error ? exception.message : "No se pudo guardar.");
    } finally {
      setSaving(false);
    }
  }

  if (segments.length === 0)
    return <div className="empty-note">La transcripción aparecerá cuando termine el procesamiento.</div>;

  const listId = `speakers-${meeting.id}`;

  return (
    <div className="transcript-editor">
      <div className="editor-header">
        <h3>Transcripción y hablantes</h3>
        <button type="button" onClick={save} disabled={saving}>
          {saving ? "Guardando..." : "Guardar cambios"}
        </button>
      </div>
      <p className="editor-hint">
        Escribe el nombre de quien habla en cada fragmento. Las opciones se reutilizan automáticamente.
      </p>
      <datalist id={listId}>
        {speakers.map(speaker => <option key={speaker} value={speaker} />)}
      </datalist>
      {segments.map((segment, index) => (
        <div className="meeting-segment" key={segment.position}>
          <div className="meeting-segment-meta">
            <span className="timestamp">{formatTime(segment.startSeconds)}</span>
            <input
              className="speaker-input"
              list={listId}
              placeholder="Hablante"
              aria-label={`Hablante del fragmento ${index + 1}`}
              value={segment.speaker}
              onChange={event => change(index, { speaker: event.target.value })}
            />
          </div>
          <textarea
            rows={2}
            aria-label={`Texto del fragmento ${index + 1}`}
            value={segment.text}
            onChange={event => change(index, { text: event.target.value })}
          />
        </div>
      ))}
      {message && <div className="info-note">{message}</div>}
    </div>
  );
}
