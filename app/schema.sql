-- MOS-KulKul PostgreSQL schema
-- Hệ thống -> BGH/Tổ bộ môn -> Giáo viên -> Lớp -> Học sinh -> Lịch sử bài làm

CREATE TABLE IF NOT EXISTS orgs (
  id            SERIAL PRIMARY KEY,
  name          TEXT NOT NULL,
  created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS users (
  id            SERIAL PRIMARY KEY,
  username      TEXT NOT NULL UNIQUE,
  name          TEXT NOT NULL,
  role          TEXT NOT NULL CHECK (role IN ('admin', 'leadership', 'teacher', 'student')),
  password_hash TEXT NOT NULL,
  org_id        INTEGER REFERENCES orgs(id),
  created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS classes (
  id            SERIAL PRIMARY KEY,
  name          TEXT NOT NULL,
  org_id        INTEGER REFERENCES orgs(id),
  teacher_id    INTEGER REFERENCES users(id),
  curriculum    JSONB NOT NULL DEFAULT '{}'::jsonb,
  created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS enrollments (
  class_id      INTEGER NOT NULL REFERENCES classes(id) ON DELETE CASCADE,
  user_id       INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  PRIMARY KEY (class_id, user_id)
);

CREATE TABLE IF NOT EXISTS projects (
  id            TEXT PRIMARY KEY,
  title         TEXT NOT NULL,
  program       TEXT NOT NULL CHECK (program IN ('word', 'excel', 'powerpoint')),
  skill_domain  TEXT NOT NULL DEFAULT '',
  filename      TEXT NOT NULL,
  file_path     TEXT NOT NULL,
  steps         JSONB NOT NULL DEFAULT '[]'::jsonb,
  rubric        JSONB NOT NULL DEFAULT '{}'::jsonb,
  time_limit_sec INTEGER NOT NULL DEFAULT 1800,
  created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS attempts (
  id            TEXT PRIMARY KEY,
  user_id       INTEGER NOT NULL REFERENCES users(id),
  project_id    TEXT NOT NULL REFERENCES projects(id),
  class_id      INTEGER REFERENCES classes(id),
  mode          TEXT NOT NULL CHECK (mode IN ('training', 'testing')),
  status        TEXT NOT NULL DEFAULT 'running',
  score         DOUBLE PRECISION,
  max_score     DOUBLE PRECISION NOT NULL DEFAULT 100,
  retries       INTEGER NOT NULL DEFAULT 0,
  started_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
  submitted_at  TIMESTAMPTZ,
  duration_sec  INTEGER,
  result_path   TEXT,
  payload       JSONB NOT NULL DEFAULT '{}'::jsonb
);

CREATE TABLE IF NOT EXISTS telemetry (
  id            BIGSERIAL PRIMARY KEY,
  attempt_id    TEXT NOT NULL REFERENCES attempts(id) ON DELETE CASCADE,
  ts            TIMESTAMPTZ NOT NULL DEFAULT now(),
  skill         TEXT NOT NULL DEFAULT '',
  action        TEXT NOT NULL,
  detail        JSONB NOT NULL DEFAULT '{}'::jsonb
);

CREATE TABLE IF NOT EXISTS project_versions (
  id              TEXT PRIMARY KEY,
  project_id      TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
  rubric_version  TEXT NOT NULL,
  grader_schema   TEXT NOT NULL DEFAULT 'mos-kulkul-1',
  manifest        JSONB NOT NULL DEFAULT '{}'::jsonb,
  rubric          JSONB NOT NULL DEFAULT '{}'::jsonb,
  source_sha256   TEXT,
  published       BOOLEAN NOT NULL DEFAULT TRUE,
  created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE (project_id, rubric_version)
);

CREATE TABLE IF NOT EXISTS submissions (
  id                TEXT PRIMARY KEY,
  attempt_id        TEXT NOT NULL REFERENCES attempts(id) ON DELETE CASCADE,
  idempotency_key   TEXT,
  status            TEXT NOT NULL DEFAULT 'received',
  artifact_sha256   TEXT,
  snapshot_path     TEXT,
  grader_version    TEXT,
  score             DOUBLE PRECISION,
  verified_score    DOUBLE PRECISION,
  pending_score     DOUBLE PRECISION,
  max_score         DOUBLE PRECISION NOT NULL DEFAULT 100,
  payload           JSONB NOT NULL DEFAULT '{}'::jsonb,
  sealed_at         TIMESTAMPTZ,
  created_at        TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS grading_runs (
  id              TEXT PRIMARY KEY,
  submission_id   TEXT NOT NULL REFERENCES submissions(id) ON DELETE CASCADE,
  grader_version  TEXT NOT NULL,
  rubric_version  TEXT,
  status          TEXT NOT NULL DEFAULT 'provisional',
  reason          TEXT,
  started_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
  finished_at     TIMESTAMPTZ
);

CREATE TABLE IF NOT EXISTS criterion_results (
  id                BIGSERIAL PRIMARY KEY,
  grading_run_id    TEXT NOT NULL REFERENCES grading_runs(id) ON DELETE CASCADE,
  criterion_id      TEXT NOT NULL,
  status            TEXT NOT NULL,
  earned            DOUBLE PRECISION NOT NULL DEFAULT 0,
  possible          DOUBLE PRECISION NOT NULL,
  reason_code       TEXT,
  evidence_refs     JSONB NOT NULL DEFAULT '[]'::jsonb,
  message           TEXT
);

CREATE TABLE IF NOT EXISTS review_decisions (
  id                   BIGSERIAL PRIMARY KEY,
  criterion_result_id  BIGINT REFERENCES criterion_results(id) ON DELETE CASCADE,
  reviewer_id          INTEGER REFERENCES users(id),
  before_status        TEXT,
  after_status         TEXT,
  reason               TEXT,
  created_at           TIMESTAMPTZ NOT NULL DEFAULT now()
);

ALTER TABLE projects ADD COLUMN IF NOT EXISTS rubric_version TEXT NOT NULL DEFAULT 'legacy';
ALTER TABLE attempts ADD COLUMN IF NOT EXISTS project_version_id TEXT;
ALTER TABLE attempts ADD COLUMN IF NOT EXISTS verified_score DOUBLE PRECISION;
ALTER TABLE attempts ADD COLUMN IF NOT EXISTS pending_score DOUBLE PRECISION;
ALTER TABLE attempts DROP CONSTRAINT IF EXISTS attempts_status_check;
ALTER TABLE attempts ADD CONSTRAINT attempts_status_check
  CHECK (status IN ('running', 'submitted', 'graded', 'technical_error'));
ALTER TABLE telemetry ADD COLUMN IF NOT EXISTS event_id TEXT;
ALTER TABLE telemetry ADD COLUMN IF NOT EXISTS sequence INTEGER;
ALTER TABLE telemetry ADD COLUMN IF NOT EXISTS document_id TEXT;
ALTER TABLE telemetry ADD COLUMN IF NOT EXISTS source_version TEXT;

CREATE UNIQUE INDEX IF NOT EXISTS submissions_attempt_idempotency
  ON submissions (attempt_id, idempotency_key)
  WHERE idempotency_key IS NOT NULL AND idempotency_key <> '';
CREATE UNIQUE INDEX IF NOT EXISTS telemetry_attempt_event_id
  ON telemetry (attempt_id, event_id)
  WHERE event_id IS NOT NULL AND event_id <> '';
CREATE INDEX IF NOT EXISTS idx_attempts_user ON attempts(user_id, started_at DESC);
CREATE INDEX IF NOT EXISTS idx_attempts_class ON attempts(class_id);
CREATE INDEX IF NOT EXISTS idx_telemetry_attempt ON telemetry(attempt_id);
CREATE INDEX IF NOT EXISTS idx_enrollments_user ON enrollments(user_id);
CREATE INDEX IF NOT EXISTS idx_submissions_attempt ON submissions(attempt_id);
CREATE INDEX IF NOT EXISTS idx_criterion_run ON criterion_results(grading_run_id);
