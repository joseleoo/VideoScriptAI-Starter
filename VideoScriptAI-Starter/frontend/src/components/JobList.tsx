import type { Job } from "../types";

type Props = {
  jobs: Job[];
  selectedId: string | null;
  onSelect: (id: string) => void;
};

const activeStatuses = new Set(["Pending", "Validating", "ExtractingAudio", "Transcribing", "Exporting"]);

export default function JobList({ jobs, selectedId, onSelect }: Props) {
  if (jobs.length === 0)
    return <div className="empty-note">Todavía no hay transcripciones.</div>;

  return (
    <div className="job-list">
      {jobs.map((job) => (
        <button
          type="button"
          key={job.id}
          className={`job-row ${job.id === selectedId ? "selected" : ""}`}
          onClick={() => onSelect(job.id)}
        >
          <div className="job-row-top">
            <strong title={job.originalFileName}>{job.originalFileName}</strong>
            <span className={`badge status-${job.status.toLowerCase()}`}>{job.status}</span>
          </div>
          <div className="job-row-meta">
            <span>{job.model}</span>
            <span>{new Date(job.createdAtUtc).toLocaleString()}</span>
          </div>
          <div className="progress-track">
            <div className={`progress-fill ${activeStatuses.has(job.status) ? "active" : ""}`} style={{ width: `${job.progress}%` }} />
          </div>
        </button>
      ))}
    </div>
  );
}
