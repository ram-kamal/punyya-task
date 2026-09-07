-- ==========================================================
-- PHF Work Management System - Initial PostgreSQL 16+ Schema
-- ==========================================================

CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Users & Stations
CREATE TABLE IF NOT EXISTS users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email VARCHAR(255) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    full_name VARCHAR(255) NOT NULL,
    role VARCHAR(50) NOT NULL CHECK (role IN ('ADMIN', 'MANAGER', 'EMPLOYEE')),
    station_id VARCHAR(50),
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Employee Work Shifts (Clock In / Clock Out)
CREATE TABLE IF NOT EXISTS shifts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    station_id VARCHAR(50) NOT NULL,
    clock_in TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    clock_out TIMESTAMPTZ,
    total_active_seconds INT DEFAULT 0,
    total_idle_seconds INT DEFAULT 0,
    status VARCHAR(50) DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE', 'COMPLETED')),
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Tasks
CREATE TABLE IF NOT EXISTS tasks (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    task_code VARCHAR(50) UNIQUE NOT NULL,
    title VARCHAR(255) NOT NULL,
    description TEXT,
    assigned_to UUID REFERENCES users(id),
    assigned_by UUID REFERENCES users(id),
    status VARCHAR(50) DEFAULT 'ASSIGNED' CHECK (status IN ('ASSIGNED', 'IN_PROGRESS', 'UNDER_REVIEW', 'COMPLETED')),
    priority VARCHAR(20) DEFAULT 'MEDIUM' CHECK (priority IN ('LOW', 'MEDIUM', 'HIGH', 'CRITICAL')),
    deliverable_required BOOLEAN DEFAULT TRUE,
    deliverable_url TEXT,
    deliverable_file_name VARCHAR(255),
    due_date TIMESTAMPTZ,
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Task Attachments / Deliverables
CREATE TABLE IF NOT EXISTS task_attachments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    task_id UUID NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
    uploaded_by UUID REFERENCES users(id),
    file_name VARCHAR(255) NOT NULL,
    storage_path TEXT NOT NULL,
    file_size_bytes BIGINT NOT NULL,
    mime_type VARCHAR(100),
    uploaded_at TIMESTAMPTZ DEFAULT NOW()
);

-- System Sessions (Logon / Logoff / Lock / Unlock)
CREATE TABLE IF NOT EXISTS user_sessions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID REFERENCES users(id),
    machine_name VARCHAR(100) NOT NULL,
    event_type VARCHAR(50) NOT NULL CHECK (event_type IN ('LOGON', 'LOGOFF', 'LOCK', 'UNLOCK')),
    event_timestamp TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Application Activity Logs
CREATE TABLE IF NOT EXISTS app_activity_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID REFERENCES users(id),
    task_id UUID REFERENCES tasks(id),
    process_name VARCHAR(255) NOT NULL,
    window_title TEXT,
    duration_seconds INT NOT NULL,
    is_idle BOOLEAN DEFAULT FALSE,
    logged_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Performance Indexes
CREATE INDEX IF NOT EXISTS idx_users_station_id ON users(station_id);
CREATE INDEX IF NOT EXISTS idx_shifts_user_clock_in ON shifts(user_id, clock_in DESC);
CREATE INDEX IF NOT EXISTS idx_tasks_assigned_to ON tasks(assigned_to, status);
CREATE INDEX IF NOT EXISTS idx_tasks_task_code ON tasks(task_code);
CREATE INDEX IF NOT EXISTS idx_task_attachments_task_id ON task_attachments(task_id);
CREATE INDEX IF NOT EXISTS idx_user_sessions_user_date ON user_sessions(user_id, event_timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_app_activity_logs_user_date ON app_activity_logs(user_id, logged_at DESC);
CREATE INDEX IF NOT EXISTS idx_app_activity_logs_task ON app_activity_logs(task_id);

-- Seed Initial Users (Password: Admin@123 / Password@123)
-- BCrypt hashes:
-- $2a$11$qR07mJ0pQ/4N3z3d.QhJquF6.fO/xYjI7w2Zl9xG6vP0s9z9Xz9X2 -> Password@123
INSERT INTO users (id, email, password_hash, full_name, role, station_id, is_active)
VALUES
    ('a0000000-0000-0000-0000-000000000001', 'admin@punnyaheart.org', '$2a$11$A5n9wQnFkJ7.p9v5eF74CeH26K1p6oU5rB6/qSgX1vT6S8Hk5B6G2', 'System Administrator', 'ADMIN', 'ST-ADMIN', TRUE),
    ('a0000000-0000-0000-0000-000000000002', 'manager@punnyaheart.org', '$2a$11$A5n9wQnFkJ7.p9v5eF74CeH26K1p6oU5rB6/qSgX1vT6S8Hk5B6G2', 'Operations Manager', 'MANAGER', 'ST-MGR', TRUE),
    ('a0000000-0000-0000-0000-000000000003', 'st101@punnyaheart.org', '$2a$11$A5n9wQnFkJ7.p9v5eF74CeH26K1p6oU5rB6/qSgX1vT6S8Hk5B6G2', 'Aarav Sharma', 'EMPLOYEE', 'ST-101', TRUE),
    ('a0000000-0000-0000-0000-000000000004', 'st102@punnyaheart.org', '$2a$11$A5n9wQnFkJ7.p9v5eF74CeH26K1p6oU5rB6/qSgX1vT6S8Hk5B6G2', 'Priya Patel', 'EMPLOYEE', 'ST-102', TRUE),
    ('a0000000-0000-0000-0000-000000000005', 'st103@punnyaheart.org', '$2a$11$A5n9wQnFkJ7.p9v5eF74CeH26K1p6oU5rB6/qSgX1vT6S8Hk5B6G2', 'Rahul Verma', 'EMPLOYEE', 'ST-103', TRUE),
    ('a0000000-0000-0000-0000-000000000006', 'st104@punnyaheart.org', '$2a$11$A5n9wQnFkJ7.p9v5eF74CeH26K1p6oU5rB6/qSgX1vT6S8Hk5B6G2', 'Sneha Reddy', 'EMPLOYEE', 'ST-104', TRUE)
ON CONFLICT (email) DO NOTHING;

-- Seed Initial Tasks
INSERT INTO tasks (id, task_code, title, description, assigned_to, assigned_by, status, priority, deliverable_required, deliverable_file_name, deliverable_url, started_at, completed_at)
VALUES
    ('b0000000-0000-0000-0000-000000000001', 'TASK-108', 'Configure Backup Scripts', 'Automate daily database snapshot replication to cloud object storage.', 'a0000000-0000-0000-0000-000000000003', 'a0000000-0000-0000-0000-000000000002', 'COMPLETED', 'HIGH', TRUE, 'backup_config.zip', '/storage/deliverables/backup_config.zip', NOW() - INTERVAL '6 HOURS', NOW() - INTERVAL '1 HOUR'),
    ('b0000000-0000-0000-0000-000000000002', 'TASK-109', 'Edit Diet Education Video', 'Color grade and splice b-roll footage for patient cardiac diet regimen.', 'a0000000-0000-0000-0000-000000000003', 'a0000000-0000-0000-0000-000000000002', 'IN_PROGRESS', 'CRITICAL', TRUE, NULL, NULL, NOW() - INTERVAL '3 HOURS', NULL),
    ('b0000000-0000-0000-0000-000000000003', 'TASK-110', 'Draft Technical Support Guide', 'Write comprehensive troubleshooting documentation for new telemetry workstations.', 'a0000000-0000-0000-0000-000000000004', 'a0000000-0000-0000-0000-000000000002', 'ASSIGNED', 'MEDIUM', TRUE, NULL, NULL, NULL, NULL)
ON CONFLICT (task_code) DO NOTHING;
