-- MOS-KulKul PostgreSQL schema
-- Hệ thống -> admin -> Giáo viên -> Lớp -> Học sinh -> Lịch sử bài làm

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
  CHECK (status IN ('running', 'submitted', 'graded', 'abandoned', 'technical_error'));
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

-- Học sinh, bài tập, tiến độ, tiến bộ, đánh giá
ALTER TABLE users ADD COLUMN IF NOT EXISTS student_code TEXT;
ALTER TABLE users ADD COLUMN IF NOT EXISTS last_seen_at TIMESTAMPTZ;
ALTER TABLE users ADD COLUMN IF NOT EXISTS last_client TEXT;
ALTER TABLE users ADD COLUMN IF NOT EXISTS is_active BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE enrollments ADD COLUMN IF NOT EXISTS enrolled_at TIMESTAMPTZ NOT NULL DEFAULT now();
ALTER TABLE enrollments ADD COLUMN IF NOT EXISTS dropped_at TIMESTAMPTZ;
CREATE TABLE IF NOT EXISTS org_settings (
  key   TEXT PRIMARY KEY,
  value TEXT NOT NULL DEFAULT ''
);
ALTER TABLE attempts ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ NOT NULL DEFAULT now();
ALTER TABLE attempts ADD COLUMN IF NOT EXISTS client_updated_at TIMESTAMPTZ;
ALTER TABLE attempts ADD COLUMN IF NOT EXISTS progress_pct INTEGER NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS q_matrix_results (
  id            BIGSERIAL PRIMARY KEY,
  attempt_id    TEXT NOT NULL REFERENCES attempts(id) ON DELETE CASCADE,
  criterion_id  TEXT NOT NULL,
  locate        TEXT NOT NULL DEFAULT '',
  tool          TEXT NOT NULL DEFAULT '',
  configure     TEXT NOT NULL DEFAULT '',
  status        TEXT NOT NULL DEFAULT '',
  earned        DOUBLE PRECISION NOT NULL DEFAULT 0,
  possible      DOUBLE PRECISION NOT NULL DEFAULT 0,
  created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_qmatrix_attempt ON q_matrix_results(attempt_id);

CREATE OR REPLACE VIEW exam_sessions AS
SELECT
  a.id AS session_id,
  a.user_id AS student_id,
  a.project_id AS exam_id,
  a.class_id,
  CASE a.status
    WHEN 'running' THEN 'IN_PROGRESS'
    WHEN 'submitted' THEN 'SUBMITTED'
    WHEN 'graded' THEN 'SUBMITTED'
    WHEN 'abandoned' THEN 'ABANDONED'
    ELSE upper(a.status)
  END AS status,
  a.progress_pct,
  a.mode,
  a.score,
  a.verified_score,
  a.updated_at,
  a.client_updated_at,
  a.started_at,
  a.submitted_at
FROM attempts a;
ALTER TABLE enrollments ADD COLUMN IF NOT EXISTS enrolled_at TIMESTAMPTZ NOT NULL DEFAULT now();
ALTER TABLE projects ADD COLUMN IF NOT EXISTS objective TEXT NOT NULL DEFAULT '';
ALTER TABLE projects ADD COLUMN IF NOT EXISTS sort_order INTEGER NOT NULL DEFAULT 0;
ALTER TABLE projects ADD COLUMN IF NOT EXISTS published BOOLEAN NOT NULL DEFAULT TRUE;

CREATE UNIQUE INDEX IF NOT EXISTS users_student_code
  ON users (student_code)
  WHERE student_code IS NOT NULL AND student_code <> '';

CREATE TABLE IF NOT EXISTS assignments (
  id            SERIAL PRIMARY KEY,
  project_id    TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
  class_id      INTEGER NOT NULL REFERENCES classes(id) ON DELETE CASCADE,
  assigned_by   INTEGER REFERENCES users(id),
  mode          TEXT NOT NULL DEFAULT 'training' CHECK (mode IN ('training', 'testing')),
  due_at        TIMESTAMPTZ,
  created_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE (project_id, class_id)
);

CREATE TABLE IF NOT EXISTS assignment_students (
  assignment_id INTEGER NOT NULL REFERENCES assignments(id) ON DELETE CASCADE,
  user_id       INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  PRIMARY KEY (assignment_id, user_id)
);

CREATE TABLE IF NOT EXISTS exercise_progress (
  user_id           INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  project_id        TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
  class_id          INTEGER REFERENCES classes(id),
  assignment_id     INTEGER REFERENCES assignments(id) ON DELETE SET NULL,
  attempt_id        TEXT REFERENCES attempts(id) ON DELETE SET NULL,
  status            TEXT NOT NULL DEFAULT 'not_started'
                    CHECK (status IN ('not_started', 'in_progress', 'submitted', 'mastered')),
  best_verified     DOUBLE PRECISION NOT NULL DEFAULT 0,
  first_verified    DOUBLE PRECISION,
  last_verified     DOUBLE PRECISION,
  last_pending      DOUBLE PRECISION,
  last_score        DOUBLE PRECISION,
  attempt_count     INTEGER NOT NULL DEFAULT 0,
  checkpoint_count  INTEGER NOT NULL DEFAULT 0,
  evidence_count    INTEGER NOT NULL DEFAULT 0,
  criteria_passed   INTEGER NOT NULL DEFAULT 0,
  criteria_total    INTEGER NOT NULL DEFAULT 0,
  growth            DOUBLE PRECISION NOT NULL DEFAULT 0,
  first_started_at  TIMESTAMPTZ,
  last_activity_at  TIMESTAMPTZ,
  completed_at      TIMESTAMPTZ,
  mastery_at        TIMESTAMPTZ,
  payload           JSONB NOT NULL DEFAULT '{}'::jsonb,
  PRIMARY KEY (user_id, project_id)
);

CREATE TABLE IF NOT EXISTS skill_progress (
  user_id         INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  project_id      TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
  criterion_id    TEXT NOT NULL,
  skill_label     TEXT NOT NULL DEFAULT '',
  status          TEXT NOT NULL DEFAULT 'unverified',
  earned          DOUBLE PRECISION NOT NULL DEFAULT 0,
  possible        DOUBLE PRECISION NOT NULL DEFAULT 0,
  attempts        INTEGER NOT NULL DEFAULT 0,
  first_pass_at   TIMESTAMPTZ,
  last_attempt_id TEXT,
  updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
  PRIMARY KEY (user_id, project_id, criterion_id)
);

CREATE TABLE IF NOT EXISTS progress_events (
  id               BIGSERIAL PRIMARY KEY,
  user_id          INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  project_id       TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
  attempt_id       TEXT REFERENCES attempts(id) ON DELETE SET NULL,
  event            TEXT NOT NULL,
  verified         DOUBLE PRECISION,
  pending          DOUBLE PRECISION,
  score            DOUBLE PRECISION,
  criteria_passed  INTEGER,
  criteria_total   INTEGER,
  detail           JSONB NOT NULL DEFAULT '{}'::jsonb,
  created_at       TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS student_evaluations (
  user_id              INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  program              TEXT NOT NULL DEFAULT 'word',
  class_id             INTEGER REFERENCES classes(id),
  overall_score        DOUBLE PRECISION,
  exercises_assigned   INTEGER NOT NULL DEFAULT 0,
  exercises_started    INTEGER NOT NULL DEFAULT 0,
  exercises_completed  INTEGER NOT NULL DEFAULT 0,
  exercises_mastered   INTEGER NOT NULL DEFAULT 0,
  completion_pct       DOUBLE PRECISION NOT NULL DEFAULT 0,
  avg_verified         DOUBLE PRECISION,
  avg_growth           DOUBLE PRECISION,
  weak_skills          JSONB NOT NULL DEFAULT '[]'::jsonb,
  strong_skills        JSONB NOT NULL DEFAULT '[]'::jsonb,
  level                TEXT NOT NULL DEFAULT 'chua_bat_dau',
  summary              TEXT NOT NULL DEFAULT '',
  computed_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
  PRIMARY KEY (user_id, program)
);

CREATE INDEX IF NOT EXISTS idx_assignments_class ON assignments(class_id);
CREATE INDEX IF NOT EXISTS idx_progress_class ON exercise_progress(class_id);
CREATE INDEX IF NOT EXISTS idx_progress_status ON exercise_progress(status);
CREATE INDEX IF NOT EXISTS idx_progress_events_user ON progress_events(user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_skill_progress_user ON skill_progress(user_id, status);
CREATE INDEX IF NOT EXISTS idx_evaluations_level ON student_evaluations(level);

CREATE TABLE IF NOT EXISTS first_attempt_q (
  attempt_id      TEXT PRIMARY KEY REFERENCES attempts(id) ON DELETE CASCADE,
  locate_fail     INTEGER NOT NULL DEFAULT 0,
  tool_fail       INTEGER NOT NULL DEFAULT 0,
  configure_fail  INTEGER NOT NULL DEFAULT 0,
  fail_count      INTEGER NOT NULL DEFAULT 0,
  item_count      INTEGER NOT NULL DEFAULT 0,
  created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS staff_sessions (
  id            TEXT PRIMARY KEY,
  user_id       INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  started_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
  last_seen_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
  ended_at      TIMESTAMPTZ,
  client        TEXT NOT NULL DEFAULT 'web'
);

CREATE TABLE IF NOT EXISTS staff_events (
  id            BIGSERIAL PRIMARY KEY,
  user_id       INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  session_id    TEXT,
  event         TEXT NOT NULL,
  path          TEXT NOT NULL DEFAULT '',
  detail        JSONB NOT NULL DEFAULT '{}'::jsonb,
  created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_staff_events_user ON staff_events(user_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_staff_sessions_user ON staff_sessions(user_id, last_seen_at DESC);

-- Dual-role Micro-LMS: LIS roster + LTI 1.3 Tool Provider (không forum / quiz / luận)
ALTER TABLE users ADD COLUMN IF NOT EXISTS lis_sourced_id TEXT;
ALTER TABLE users ADD COLUMN IF NOT EXISTS lti_sub TEXT;
ALTER TABLE users ADD COLUMN IF NOT EXISTS lti_issuer TEXT;
ALTER TABLE classes ADD COLUMN IF NOT EXISTS lti_context_id TEXT;
ALTER TABLE classes ADD COLUMN IF NOT EXISTS lti_deployment_id TEXT;
ALTER TABLE attempts ADD COLUMN IF NOT EXISTS lti_launch_id TEXT;

CREATE TABLE IF NOT EXISTS lti_platforms (
  id              SERIAL PRIMARY KEY,
  name            TEXT NOT NULL DEFAULT '',
  issuer          TEXT NOT NULL UNIQUE,
  client_id       TEXT NOT NULL,
  auth_login_url  TEXT NOT NULL,
  auth_token_url  TEXT NOT NULL DEFAULT '',
  jwks_url        TEXT NOT NULL DEFAULT '',
  jwks_json       JSONB,
  created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS lti_deployments (
  platform_id     INTEGER NOT NULL REFERENCES lti_platforms(id) ON DELETE CASCADE,
  deployment_id   TEXT NOT NULL,
  PRIMARY KEY (platform_id, deployment_id)
);

CREATE TABLE IF NOT EXISTS lti_nonces (
  state           TEXT PRIMARY KEY,
  nonce           TEXT NOT NULL,
  target_link_uri TEXT NOT NULL DEFAULT '',
  login_hint      TEXT NOT NULL DEFAULT '',
  created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS lti_launches (
  id              TEXT PRIMARY KEY,
  user_id         INTEGER REFERENCES users(id) ON DELETE SET NULL,
  platform_id     INTEGER REFERENCES lti_platforms(id) ON DELETE SET NULL,
  deployment_id   TEXT NOT NULL DEFAULT '',
  context_id      TEXT NOT NULL DEFAULT '',
  resource_link_id TEXT NOT NULL DEFAULT '',
  project_id      TEXT,
  lineitem        TEXT NOT NULL DEFAULT '',
  ags_scopes      JSONB NOT NULL DEFAULT '[]'::jsonb,
  lti_sub         TEXT NOT NULL DEFAULT '',
  created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_users_lti_sub
  ON users (lti_issuer, lti_sub)
  WHERE lti_issuer IS NOT NULL AND lti_sub IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_lti_launches_user ON lti_launches(user_id, created_at DESC);

-- Command Center: roster join-code, assignment LAN/adaptive, telemetry indexes
ALTER TABLE classes ADD COLUMN IF NOT EXISTS join_code TEXT;
CREATE UNIQUE INDEX IF NOT EXISTS idx_classes_join_code ON classes(join_code) WHERE join_code IS NOT NULL AND join_code <> '';

ALTER TABLE assignments ADD COLUMN IF NOT EXISTS time_limit_sec INTEGER;
ALTER TABLE assignments ADD COLUMN IF NOT EXISTS ip_allow TEXT NOT NULL DEFAULT '';
ALTER TABLE assignments ADD COLUMN IF NOT EXISTS unlock_below DOUBLE PRECISION;
ALTER TABLE assignments ADD COLUMN IF NOT EXISTS unlock_project_id TEXT;

CREATE TABLE IF NOT EXISTS adaptive_rules (
  id                 SERIAL PRIMARY KEY,
  class_id           INTEGER NOT NULL REFERENCES classes(id) ON DELETE CASCADE,
  source_project_id  TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
  below_score        DOUBLE PRECISION NOT NULL DEFAULT 50,
  unlock_project_id  TEXT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
  created_by         INTEGER REFERENCES users(id),
  created_at         TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE (class_id, source_project_id, unlock_project_id)
);

CREATE INDEX IF NOT EXISTS idx_telemetry_ts ON telemetry(ts DESC);
CREATE INDEX IF NOT EXISTS idx_telemetry_attempt_ts ON telemetry(attempt_id, ts DESC);
CREATE INDEX IF NOT EXISTS idx_attempts_updated ON attempts(updated_at DESC);

-- Ngân hàng đề 3 tầng: Objective → Project → Task → Exam (không đụng bảng projects cũ)
CREATE TABLE IF NOT EXISTS objective_domains (
  id            TEXT PRIMARY KEY,
  subject       TEXT NOT NULL CHECK (subject IN ('MO-100', 'MO-200', 'MO-300')),
  code          TEXT NOT NULL,
  title         TEXT NOT NULL DEFAULT '',
  description   TEXT NOT NULL DEFAULT '',
  parent_id     TEXT REFERENCES objective_domains(id) ON DELETE CASCADE,
  sort_order    INTEGER NOT NULL DEFAULT 0,
  UNIQUE (subject, code)
);

CREATE TABLE IF NOT EXISTS bank_tasks (
  id                TEXT PRIMARY KEY,
  objective_id      TEXT REFERENCES objective_domains(id) ON DELETE SET NULL,
  instruction_text  TEXT NOT NULL DEFAULT '',
  q_matrix_rules    JSONB NOT NULL DEFAULT '{}'::jsonb,
  default_weight    INTEGER NOT NULL DEFAULT 1,
  status            TEXT NOT NULL DEFAULT 'draft'
                    CHECK (status IN ('draft', 'published', 'archived')),
  source_project_id TEXT REFERENCES projects(id) ON DELETE SET NULL,
  resource_file     TEXT NOT NULL DEFAULT '',
  file_sha256       TEXT,
  created_by        INTEGER REFERENCES users(id),
  is_global         BOOLEAN NOT NULL DEFAULT TRUE,
  version_hash      TEXT NOT NULL DEFAULT '',
  created_at        TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS bank_projects (
  id                    TEXT PRIMARY KEY,
  name                  TEXT NOT NULL,
  scenario_description  TEXT NOT NULL DEFAULT '',
  resource_file_url     TEXT NOT NULL DEFAULT '',
  program               TEXT NOT NULL DEFAULT 'word'
                        CHECK (program IN ('word', 'excel', 'powerpoint')),
  source_project_id     TEXT REFERENCES projects(id) ON DELETE SET NULL,
  status                TEXT NOT NULL DEFAULT 'draft'
                        CHECK (status IN ('draft', 'published', 'archived')),
  created_by            INTEGER REFERENCES users(id),
  is_global             BOOLEAN NOT NULL DEFAULT TRUE,
  created_at            TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS bank_project_tasks (
  project_id      TEXT NOT NULL REFERENCES bank_projects(id) ON DELETE CASCADE,
  task_id         TEXT NOT NULL REFERENCES bank_tasks(id) ON DELETE CASCADE,
  sequence_order  INTEGER NOT NULL DEFAULT 1,
  PRIMARY KEY (project_id, task_id)
);

CREATE TABLE IF NOT EXISTS bank_exams (
  id                TEXT PRIMARY KEY,
  title             TEXT NOT NULL,
  exam_type         TEXT NOT NULL DEFAULT 'PRACTICE_EXAM'
                    CHECK (exam_type IN ('PRACTICE_EXAM', 'CERTIFICATION_MOCK')),
  duration_minutes  INTEGER NOT NULL DEFAULT 50,
  program           TEXT NOT NULL DEFAULT 'word',
  status            TEXT NOT NULL DEFAULT 'draft'
                    CHECK (status IN ('draft', 'published', 'archived')),
  parent_exam_id    TEXT REFERENCES bank_exams(id) ON DELETE SET NULL,
  version           INTEGER NOT NULL DEFAULT 1,
  created_by        INTEGER REFERENCES users(id),
  is_global         BOOLEAN NOT NULL DEFAULT TRUE,
  version_hash      TEXT NOT NULL DEFAULT '',
  created_at        TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS bank_exam_projects (
  exam_id         TEXT NOT NULL REFERENCES bank_exams(id) ON DELETE CASCADE,
  project_id      TEXT NOT NULL REFERENCES bank_projects(id) ON DELETE CASCADE,
  project_order   INTEGER NOT NULL DEFAULT 1,
  PRIMARY KEY (exam_id, project_id)
);

CREATE TABLE IF NOT EXISTS exam_issue_flags (
  id            BIGSERIAL PRIMARY KEY,
  exam_id       TEXT REFERENCES bank_exams(id) ON DELETE CASCADE,
  task_id       TEXT REFERENCES bank_tasks(id) ON DELETE SET NULL,
  reporter_id   INTEGER REFERENCES users(id),
  detail        TEXT NOT NULL DEFAULT '',
  created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS student_task_results (
  id            BIGSERIAL PRIMARY KEY,
  student_id    INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  task_id       TEXT NOT NULL REFERENCES bank_tasks(id) ON DELETE CASCADE,
  attempt_id    TEXT REFERENCES attempts(id) ON DELETE SET NULL,
  is_correct    BOOLEAN NOT NULL DEFAULT FALSE,
  raw_earned    DOUBLE PRECISION NOT NULL DEFAULT 0,
  raw_possible  DOUBLE PRECISION NOT NULL DEFAULT 0,
  scaled_1000   INTEGER,
  created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);

ALTER TABLE assignments ADD COLUMN IF NOT EXISTS exam_id TEXT REFERENCES bank_exams(id) ON DELETE SET NULL;
ALTER TABLE assignments ADD COLUMN IF NOT EXISTS objective_id TEXT REFERENCES objective_domains(id) ON DELETE SET NULL;
ALTER TABLE assignments ADD COLUMN IF NOT EXISTS opens_at TIMESTAMPTZ;
ALTER TABLE assignments ADD COLUMN IF NOT EXISTS closes_at TIMESTAMPTZ;
ALTER TABLE bank_tasks ADD COLUMN IF NOT EXISTS high_difficulty BOOLEAN NOT NULL DEFAULT FALSE;

CREATE TABLE IF NOT EXISTS formative_telemetry (
  id            BIGSERIAL PRIMARY KEY,
  student_id    INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  task_id       TEXT REFERENCES bank_tasks(id) ON DELETE SET NULL,
  attempt_id    TEXT REFERENCES attempts(id) ON DELETE SET NULL,
  event         TEXT NOT NULL CHECK (event IN ('hint1', 'hint2', 'hint3', 'wrong_check', 'check_pass')),
  created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_bank_tasks_objective ON bank_tasks(objective_id);
CREATE INDEX IF NOT EXISTS idx_bank_tasks_status ON bank_tasks(status);
CREATE INDEX IF NOT EXISTS idx_student_task_results ON student_task_results(student_id, task_id);
CREATE INDEX IF NOT EXISTS idx_exam_flags ON exam_issue_flags(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_bank_tasks_qmatrix ON bank_tasks USING GIN (q_matrix_rules);
CREATE INDEX IF NOT EXISTS idx_formative_task ON formative_telemetry(task_id, event);

-- RLS ngân hàng đề: học sinh 0 hàng; giáo viên chỉ published; Super Admin / service đủ quyền.
-- FORCE: user kết nối (owner) không được bypass.
CREATE OR REPLACE FUNCTION mos_persona() RETURNS text
LANGUAGE sql STABLE AS $$
  SELECT COALESCE(NULLIF(current_setting('app.persona', true), ''), 'anon')
$$;

CREATE OR REPLACE FUNCTION mos_is_bank_admin() RETURNS boolean
LANGUAGE sql STABLE AS $$
  SELECT mos_persona() IN ('service', 'admin', 'leadership')
$$;

CREATE OR REPLACE FUNCTION mos_is_teacher() RETURNS boolean
LANGUAGE sql STABLE AS $$
  SELECT mos_persona() = 'teacher'
$$;

CREATE OR REPLACE FUNCTION mos_user_id() RETURNS text
LANGUAGE sql STABLE AS $$
  SELECT COALESCE(current_setting('app.user_id', true), '')
$$;

DO $$
DECLARE
  t text;
BEGIN
  FOREACH t IN ARRAY ARRAY[
    'objective_domains',
    'bank_tasks',
    'bank_projects',
    'bank_project_tasks',
    'bank_exams',
    'bank_exam_projects',
    'exam_issue_flags',
    'student_task_results',
    'formative_telemetry'
  ]
  LOOP
    EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', t);
    EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY', t);
  END LOOP;
END $$;

DROP POLICY IF EXISTS objective_domains_admin ON objective_domains;
DROP POLICY IF EXISTS objective_domains_teacher_sel ON objective_domains;
CREATE POLICY objective_domains_admin ON objective_domains
  FOR ALL USING (mos_is_bank_admin()) WITH CHECK (mos_is_bank_admin());
CREATE POLICY objective_domains_teacher_sel ON objective_domains
  FOR SELECT USING (mos_is_teacher());

DROP POLICY IF EXISTS bank_tasks_admin ON bank_tasks;
DROP POLICY IF EXISTS bank_tasks_teacher_sel ON bank_tasks;
CREATE POLICY bank_tasks_admin ON bank_tasks
  FOR ALL USING (mos_is_bank_admin()) WITH CHECK (mos_is_bank_admin());
CREATE POLICY bank_tasks_teacher_sel ON bank_tasks
  FOR SELECT USING (mos_is_teacher() AND status = 'published');

DROP POLICY IF EXISTS bank_projects_admin ON bank_projects;
DROP POLICY IF EXISTS bank_projects_teacher_sel ON bank_projects;
CREATE POLICY bank_projects_admin ON bank_projects
  FOR ALL USING (mos_is_bank_admin()) WITH CHECK (mos_is_bank_admin());
CREATE POLICY bank_projects_teacher_sel ON bank_projects
  FOR SELECT USING (mos_is_teacher() AND status = 'published');

DROP POLICY IF EXISTS bank_exams_admin ON bank_exams;
DROP POLICY IF EXISTS bank_exams_teacher_sel ON bank_exams;
CREATE POLICY bank_exams_admin ON bank_exams
  FOR ALL USING (mos_is_bank_admin()) WITH CHECK (mos_is_bank_admin());
CREATE POLICY bank_exams_teacher_sel ON bank_exams
  FOR SELECT USING (mos_is_teacher() AND status = 'published');

DROP POLICY IF EXISTS bank_project_tasks_admin ON bank_project_tasks;
DROP POLICY IF EXISTS bank_project_tasks_teacher_sel ON bank_project_tasks;
CREATE POLICY bank_project_tasks_admin ON bank_project_tasks
  FOR ALL USING (mos_is_bank_admin()) WITH CHECK (mos_is_bank_admin());
CREATE POLICY bank_project_tasks_teacher_sel ON bank_project_tasks
  FOR SELECT USING (
    mos_is_teacher() AND EXISTS (
      SELECT 1 FROM bank_projects p WHERE p.id = project_id AND p.status = 'published'
    )
  );

DROP POLICY IF EXISTS bank_exam_projects_admin ON bank_exam_projects;
DROP POLICY IF EXISTS bank_exam_projects_teacher_sel ON bank_exam_projects;
CREATE POLICY bank_exam_projects_admin ON bank_exam_projects
  FOR ALL USING (mos_is_bank_admin()) WITH CHECK (mos_is_bank_admin());
CREATE POLICY bank_exam_projects_teacher_sel ON bank_exam_projects
  FOR SELECT USING (
    mos_is_teacher() AND EXISTS (
      SELECT 1 FROM bank_exams e WHERE e.id = exam_id AND e.status = 'published'
    )
  );

DROP POLICY IF EXISTS exam_issue_flags_admin ON exam_issue_flags;
DROP POLICY IF EXISTS exam_issue_flags_teacher ON exam_issue_flags;
CREATE POLICY exam_issue_flags_admin ON exam_issue_flags
  FOR ALL USING (mos_is_bank_admin()) WITH CHECK (mos_is_bank_admin());
CREATE POLICY exam_issue_flags_teacher ON exam_issue_flags
  FOR ALL USING (mos_is_teacher()) WITH CHECK (mos_is_teacher());

DROP POLICY IF EXISTS student_task_results_staff ON student_task_results;
DROP POLICY IF EXISTS student_task_results_own ON student_task_results;
CREATE POLICY student_task_results_staff ON student_task_results
  FOR ALL USING (mos_is_bank_admin() OR mos_is_teacher())
  WITH CHECK (mos_is_bank_admin() OR mos_is_teacher());
CREATE POLICY student_task_results_own ON student_task_results
  FOR SELECT USING (
    mos_persona() = 'student' AND student_id::text = mos_user_id()
  );

DROP POLICY IF EXISTS formative_telemetry_staff ON formative_telemetry;
DROP POLICY IF EXISTS formative_telemetry_own ON formative_telemetry;
CREATE POLICY formative_telemetry_staff ON formative_telemetry
  FOR ALL USING (mos_is_bank_admin() OR mos_is_teacher())
  WITH CHECK (mos_is_bank_admin() OR mos_is_teacher());
CREATE POLICY formative_telemetry_own ON formative_telemetry
  FOR SELECT USING (
    mos_persona() = 'student' AND student_id::text = mos_user_id()
  );

-- Superuser (POSTGRES_USER trên CI) luôn bypass RLS. Role mos_app không BYPASSRLS.
-- Local / production: mos thường không superuser — FORCE RLS đủ, không cần CREATE ROLE.
DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM pg_roles
    WHERE rolname = current_user AND (rolsuper OR rolcreaterole)
  ) THEN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'mos_app') THEN
      CREATE ROLE mos_app NOINHERIT NOBYPASSRLS;
    END IF;
    ALTER ROLE mos_app NOBYPASSRLS;
    EXECUTE format('GRANT mos_app TO %I', current_user);
    GRANT USAGE ON SCHEMA public TO mos_app;
    GRANT ALL ON ALL TABLES IN SCHEMA public TO mos_app;
    GRANT ALL ON ALL SEQUENCES IN SCHEMA public TO mos_app;
    ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO mos_app;
    ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO mos_app;
  END IF;
END $$;
