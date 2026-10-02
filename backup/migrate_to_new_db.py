import psycopg2
import sys

old_conn_params = {
    'host': 'aws-0-ap-southeast-1.pooler.supabase.com',
    'port': 5432,
    'dbname': 'postgres',
    'user': 'postgres.ykarbzawnvoobmfuzvqa',
    'password': 'Punyya@2026',
    'sslmode': 'require'
}

new_conn_params = {
    'host': 'db.ghsdpgkmxrllxuwfhwnc.supabase.co',
    'port': 5432,
    'dbname': 'postgres',
    'user': 'postgres',
    'password': 'Task_Punyya@2026',
    'sslmode': 'require'
}

print("1. Connecting to source and target databases...")
src_conn = psycopg2.connect(**old_conn_params)
src_cur = src_conn.cursor()

dst_conn = psycopg2.connect(**new_conn_params)
dst_conn.autocommit = True
dst_cur = dst_conn.cursor()
print("   Connected successfully to both databases!")

print("\n2. Creating schema and tables on target database...")
schema_ddl = """
-- Extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Table: users
CREATE TABLE IF NOT EXISTS public.users (
    id UUID PRIMARY KEY,
    email VARCHAR(255) NOT NULL,
    username VARCHAR(100),
    password_hash VARCHAR(255) NOT NULL,
    full_name VARCHAR(255) NOT NULL,
    role VARCHAR(50) NOT NULL,
    station_id VARCHAR(50),
    work_mode VARCHAR(50) NOT NULL DEFAULT 'IN_OFFICE',
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT users_role_check CHECK (role IN ('ADMIN', 'MANAGER', 'MONITOR', 'EMPLOYEE'))
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_users_email" ON public.users (email);
CREATE INDEX IF NOT EXISTS "IX_users_station_id" ON public.users (station_id);

-- Table: user_sessions
CREATE TABLE IF NOT EXISTS public.user_sessions (
    id UUID PRIMARY KEY,
    user_id UUID REFERENCES public.users(id) ON DELETE CASCADE,
    machine_name VARCHAR(100) NOT NULL,
    event_type VARCHAR(50) NOT NULL,
    event_timestamp TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS "IX_user_sessions_user_id_event_timestamp" ON public.user_sessions (user_id, event_timestamp);

-- Table: tasks
CREATE TABLE IF NOT EXISTS public.tasks (
    id UUID PRIMARY KEY,
    task_code VARCHAR(50) NOT NULL,
    title VARCHAR(255) NOT NULL,
    description TEXT,
    assigned_to UUID REFERENCES public.users(id) ON DELETE SET NULL,
    assigned_by UUID REFERENCES public.users(id) ON DELETE SET NULL,
    status VARCHAR(50) NOT NULL,
    priority VARCHAR(20) NOT NULL DEFAULT 'MEDIUM',
    deliverable_required BOOLEAN NOT NULL DEFAULT true,
    deliverable_url TEXT,
    deliverable_file_name VARCHAR(255),
    due_date TIMESTAMPTZ,
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_tasks_task_code" ON public.tasks (task_code);
CREATE INDEX IF NOT EXISTS "IX_tasks_assigned_by" ON public.tasks (assigned_by);
CREATE INDEX IF NOT EXISTS "IX_tasks_assigned_to_status" ON public.tasks (assigned_to, status);

-- Table: task_attachments
CREATE TABLE IF NOT EXISTS public.task_attachments (
    id UUID PRIMARY KEY,
    task_id UUID NOT NULL REFERENCES public.tasks(id) ON DELETE CASCADE,
    uploaded_by UUID REFERENCES public.users(id) ON DELETE SET NULL,
    file_name VARCHAR(255) NOT NULL,
    storage_path TEXT NOT NULL,
    file_size_bytes BIGINT NOT NULL,
    mime_type VARCHAR(100),
    uploaded_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS "IX_task_attachments_task_id" ON public.task_attachments (task_id);
CREATE INDEX IF NOT EXISTS "IX_task_attachments_uploaded_by" ON public.task_attachments (uploaded_by);

-- Table: shifts
CREATE TABLE IF NOT EXISTS public.shifts (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL REFERENCES public.users(id) ON DELETE CASCADE,
    station_id VARCHAR(50) NOT NULL,
    clock_in TIMESTAMPTZ NOT NULL,
    clock_out TIMESTAMPTZ,
    total_active_seconds INTEGER NOT NULL DEFAULT 0,
    total_idle_seconds INTEGER NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS "IX_shifts_user_id_clock_in" ON public.shifts (user_id, clock_in);

-- Table: leave_requests
CREATE TABLE IF NOT EXISTS public.leave_requests (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL REFERENCES public.users(id) ON DELETE CASCADE,
    leave_type VARCHAR(50) NOT NULL,
    start_date TIMESTAMPTZ NOT NULL,
    end_date TIMESTAMPTZ NOT NULL,
    total_days INTEGER NOT NULL,
    reason VARCHAR(1000) NOT NULL,
    status VARCHAR(50) NOT NULL,
    reviewed_by UUID REFERENCES public.users(id) ON DELETE SET NULL,
    reviewed_at TIMESTAMPTZ,
    review_comments VARCHAR(1000),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS "IX_leave_requests_reviewed_by" ON public.leave_requests (reviewed_by);
CREATE INDEX IF NOT EXISTS "IX_leave_requests_start_date_end_date" ON public.leave_requests (start_date, end_date);
CREATE INDEX IF NOT EXISTS "IX_leave_requests_user_id_status" ON public.leave_requests (user_id, status);

-- Table: app_activity_logs
CREATE TABLE IF NOT EXISTS public.app_activity_logs (
    id UUID PRIMARY KEY,
    user_id UUID REFERENCES public.users(id) ON DELETE CASCADE,
    task_id UUID REFERENCES public.tasks(id) ON DELETE SET NULL,
    process_name VARCHAR(255) NOT NULL,
    window_title TEXT,
    duration_seconds INTEGER NOT NULL,
    is_idle BOOLEAN NOT NULL DEFAULT false,
    logged_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS "IX_app_activity_logs_task_id" ON public.app_activity_logs (task_id);
CREATE INDEX IF NOT EXISTS "IX_app_activity_logs_user_id_logged_at" ON public.app_activity_logs (user_id, logged_at);
CREATE INDEX IF NOT EXISTS ix_app_activity_logs_logged_at ON public.app_activity_logs (logged_at);
"""

dst_cur.execute(schema_ddl)
print("   All tables, constraints, and indexes created on target database!")

print("\n3. Migrating data in dependency order...")
ordered_tables = [
    'users',
    'user_sessions',
    'tasks',
    'task_attachments',
    'shifts',
    'leave_requests',
    'app_activity_logs'
]

for table in ordered_tables:
    src_cur.execute(f'SELECT * FROM public."{table}";')
    colnames = [desc[0] for desc in src_cur.description]
    col_str = ', '.join([f'"{c}"' for c in colnames])
    placeholders = ', '.join(['%s'] * len(colnames))
    rows = src_cur.fetchall()
    
    if rows:
        insert_query = f'INSERT INTO public."{table}" ({col_str}) VALUES ({placeholders}) ON CONFLICT DO NOTHING;'
        dst_cur.executemany(insert_query, rows)
        print(f"   Transferred {len(rows)} rows into {table}.")
    else:
        print(f"   Table {table} has 0 rows (skipped).")

print("\n4. Verifying target database row counts...")
for table in ordered_tables:
    dst_cur.execute(f'SELECT COUNT(*) FROM public."{table}";')
    count = dst_cur.fetchone()[0]
    print(f"   {table}: {count} rows verified")

dst_cur.execute("SELECT pg_size_pretty(pg_database_size('postgres'));")
print(f"\nTarget Database Size: {dst_cur.fetchone()[0]}")
print("MIGRATION COMPLETED SUCCESSFULLY!")
