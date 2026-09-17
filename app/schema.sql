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
  status        TEXT NOT NULL DEFAULT 'running' CHECK (status IN ('running', 'submitted')),
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

CREATE INDEX IF NOT EXISTS idx_attempts_user ON attempts(user_id, started_at DESC);
CREATE INDEX IF NOT EXISTS idx_attempts_class ON attempts(class_id);
CREATE INDEX IF NOT EXISTS idx_telemetry_attempt ON telemetry(attempt_id);
CREATE INDEX IF NOT EXISTS idx_enrollments_user ON enrollments(user_id);
