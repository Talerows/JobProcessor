-- =============================================================================
-- Migration: 001_create_jobs_table
-- Creates the jobs table required by JobProcessor.Worker.
-- =============================================================================

CREATE TABLE IF NOT EXISTS jobs
(
    id           UUID         NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
    name         TEXT         NOT NULL,
    status       TEXT         NOT NULL DEFAULT 'Open'
                              CHECK (status IN ('Open', 'InProgress', 'Completed', 'Timeout')),
    created_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    started_at   TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    timed_out_at TIMESTAMPTZ
);

-- Index to speed up the polling query (WHERE status = 'Open' ORDER BY created_at)
CREATE INDEX IF NOT EXISTS idx_jobs_status_created ON jobs (status, created_at)
    WHERE status = 'Open';

-- =============================================================================
-- Seed data – insert a few open jobs for manual testing
-- =============================================================================
INSERT INTO jobs (name) VALUES
    ('Report Generation'),
    ('Data Export'),
    ('Email Campaign'),
    ('Invoice Processing'),
    ('File Archival'),
    ('Analytics Sync'),
    ('Notification Dispatch'),
    ('Cache Warm-Up'),
    ('Audit Log Rotation'),
    ('Payment Reconciliation');
