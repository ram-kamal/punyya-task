import psycopg2
import os

conn = psycopg2.connect(
    host='aws-0-ap-southeast-1.pooler.supabase.com',
    port=5432,
    dbname='postgres',
    user='postgres.ykarbzawnvoobmfuzvqa',
    password='Punyya@2026',
    sslmode='require'
)
cur = conn.cursor()

tables = ['users', 'user_sessions', 'tasks', 'task_attachments', 'shifts', 'leave_requests', 'app_activity_logs']
out_dir = 'd:/punnya heart/punnya task management system/backup'
os.makedirs(out_dir, exist_ok=True)
sql_file = os.path.join(out_dir, 'full_database_backup.sql')

with open(sql_file, 'w', encoding='utf-8') as f:
    f.write('-- PHF Work Management Complete Database Backup\n\n')
    for table in tables:
        cur.execute(f'SELECT * FROM "{table}";')
        colnames = [desc[0] for desc in cur.description]
        col_list = ', '.join([f'"{c}"' for c in colnames])
        rows = cur.fetchall()
        
        f.write(f'-- Table: {table} ({len(rows)} rows)\n')
        for r in rows:
            vals = []
            for v in r:
                if v is None:
                    vals.append('NULL')
                elif isinstance(v, (int, float)):
                    vals.append(str(v))
                elif isinstance(v, bool):
                    vals.append('TRUE' if v else 'FALSE')
                else:
                    escaped = str(v).replace("'", "''")
                    vals.append(f"'{escaped}'")
            val_str = ', '.join(vals)
            f.write(f'INSERT INTO "{table}" ({col_list}) VALUES ({val_str}) ON CONFLICT DO NOTHING;\n')
        f.write('\n')

print(f"Full SQL script generated successfully at: {sql_file}")
