using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using PHF.Core.DTOs;

namespace PHF.Client.Core.Storage;

public class TamperProofDatabase : IDisposable
{
    private readonly string _connectionString;
    private readonly byte[] _hmacKey;
    private readonly object _dbLock = new();

    public record QueuedActivityLog(
        long Id, 
        string? UserId, 
        string? TaskId, 
        string? TaskCode, 
        string ProcessName, 
        string? WindowTitle, 
        int DurationSeconds, 
        bool IsIdle, 
        DateTime LoggedAt,
        bool IsTampered);

    public record QueuedSession(
        long Id,
        string? UserId,
        string MachineName,
        string EventType,
        DateTime EventTimestamp,
        bool IsTampered);

    public TamperProofDatabase(string? dbPath = null)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(localAppData, "PHFWorkManagement");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var fullDbPath = dbPath ?? Path.Combine(dir, "telemetry_offline.db");
        _connectionString = $"Data Source={fullDbPath};";

        // Tamper-lock integrity secret
        _hmacKey = Encoding.UTF8.GetBytes("PHF_WORK_MANAGEMENT_TAMPER_LOCK_INTEGRITY_SALT_2026_X9!");

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        lock (_dbLock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS offline_activity_logs (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    user_id TEXT,
                    task_id TEXT,
                    task_code TEXT,
                    process_name TEXT NOT NULL,
                    window_title TEXT,
                    duration_seconds INTEGER NOT NULL,
                    is_idle INTEGER NOT NULL,
                    logged_at TEXT NOT NULL,
                    payload_hash TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS offline_sessions (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    user_id TEXT,
                    machine_name TEXT NOT NULL,
                    event_type TEXT NOT NULL,
                    event_timestamp TEXT NOT NULL,
                    payload_hash TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_offline_act_id ON offline_activity_logs(id);
                CREATE INDEX IF NOT EXISTS idx_offline_sess_id ON offline_sessions(id);
            ";
            cmd.ExecuteNonQuery();
        }
    }

    public void EnqueueActivityLog(
        string? userId, 
        Guid? taskId, 
        string? taskCode, 
        string processName, 
        string? windowTitle, 
        int durationSeconds, 
        bool isIdle, 
        DateTime loggedAt)
    {
        var loggedAtIso = loggedAt.ToString("o");
        var hash = ComputeActivityHash(processName, durationSeconds, isIdle ? 1 : 0, loggedAtIso);

        lock (_dbLock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO offline_activity_logs 
                (user_id, task_id, task_code, process_name, window_title, duration_seconds, is_idle, logged_at, payload_hash)
                VALUES (@userId, @taskId, @taskCode, @processName, @windowTitle, @duration, @isIdle, @loggedAt, @hash);
            ";

            cmd.Parameters.AddWithValue("@userId", (object?)userId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@taskId", taskId.HasValue ? taskId.Value.ToString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@taskCode", (object?)taskCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@processName", processName);
            cmd.Parameters.AddWithValue("@windowTitle", (object?)windowTitle ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@duration", durationSeconds);
            cmd.Parameters.AddWithValue("@isIdle", isIdle ? 1 : 0);
            cmd.Parameters.AddWithValue("@loggedAt", loggedAtIso);
            cmd.Parameters.AddWithValue("@hash", hash);

            cmd.ExecuteNonQuery();
        }
    }

    public void EnqueueSessionEvent(string? userId, string machineName, string eventType, DateTime eventTimestamp)
    {
        var timestampIso = eventTimestamp.ToString("o");
        var hash = ComputeSessionHash(machineName, eventType, timestampIso);

        lock (_dbLock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO offline_sessions 
                (user_id, machine_name, event_type, event_timestamp, payload_hash)
                VALUES (@userId, @machineName, @eventType, @timestamp, @hash);
            ";

            cmd.Parameters.AddWithValue("@userId", (object?)userId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@machineName", machineName);
            cmd.Parameters.AddWithValue("@eventType", eventType);
            cmd.Parameters.AddWithValue("@timestamp", timestampIso);
            cmd.Parameters.AddWithValue("@hash", hash);

            cmd.ExecuteNonQuery();
        }
    }

    public List<QueuedActivityLog> GetPendingActivityLogs(int maxCount = 50)
    {
        var list = new List<QueuedActivityLog>();

        lock (_dbLock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT id, user_id, task_id, task_code, process_name, window_title, duration_seconds, is_idle, logged_at, payload_hash
                FROM offline_activity_logs
                ORDER BY id ASC
                LIMIT @limit;
            ";
            cmd.Parameters.AddWithValue("@limit", maxCount);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetInt64(0);
                var userId = reader.IsDBNull(1) ? null : reader.GetString(1);
                var taskId = reader.IsDBNull(2) ? null : reader.GetString(2);
                var taskCode = reader.IsDBNull(3) ? null : reader.GetString(3);
                var processName = reader.GetString(4);
                var windowTitle = reader.IsDBNull(5) ? null : reader.GetString(5);
                var duration = reader.GetInt32(6);
                var isIdle = reader.GetInt32(7) == 1;
                var loggedAtStr = reader.GetString(8);
                var storedHash = reader.GetString(9);

                DateTime.TryParse(loggedAtStr, out var loggedAt);

                // Tamper-lock check: verify hash integrity
                var expectedHash = ComputeActivityHash(processName, duration, isIdle ? 1 : 0, loggedAtStr);
                var isTampered = !string.Equals(storedHash, expectedHash, StringComparison.Ordinal);

                list.Add(new QueuedActivityLog(id, userId, taskId, taskCode, processName, windowTitle, duration, isIdle, loggedAt, isTampered));
            }
        }

        return list;
    }

    public List<QueuedSession> GetPendingSessions(int maxCount = 50)
    {
        var list = new List<QueuedSession>();

        lock (_dbLock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT id, user_id, machine_name, event_type, event_timestamp, payload_hash
                FROM offline_sessions
                ORDER BY id ASC
                LIMIT @limit;
            ";
            cmd.Parameters.AddWithValue("@limit", maxCount);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetInt64(0);
                var userId = reader.IsDBNull(1) ? null : reader.GetString(1);
                var machine = reader.GetString(2);
                var eventType = reader.GetString(3);
                var timestampStr = reader.GetString(4);
                var storedHash = reader.GetString(5);

                DateTime.TryParse(timestampStr, out var timestamp);

                var expectedHash = ComputeSessionHash(machine, eventType, timestampStr);
                var isTampered = !string.Equals(storedHash, expectedHash, StringComparison.Ordinal);

                list.Add(new QueuedSession(id, userId, machine, eventType, timestamp, isTampered));
            }
        }

        return list;
    }

    public void DeleteProcessedActivityLogs(IEnumerable<long> ids)
    {
        var idList = ids.ToList();
        if (!idList.Any()) return;

        lock (_dbLock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var inClause = string.Join(",", idList);
            var cmd = connection.CreateCommand();
            cmd.CommandText = $"DELETE FROM offline_activity_logs WHERE id IN ({inClause});";
            cmd.ExecuteNonQuery();
        }
    }

    public void DeleteProcessedSessions(IEnumerable<long> ids)
    {
        var idList = ids.ToList();
        if (!idList.Any()) return;

        lock (_dbLock)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var inClause = string.Join(",", idList);
            var cmd = connection.CreateCommand();
            cmd.CommandText = $"DELETE FROM offline_sessions WHERE id IN ({inClause});";
            cmd.ExecuteNonQuery();
        }
    }

    private string ComputeActivityHash(string process, int duration, int isIdle, string loggedAt)
    {
        using var hmac = new HMACSHA256(_hmacKey);
        var payload = $"{process}|{duration}|{isIdle}|{loggedAt}";
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }

    private string ComputeSessionHash(string machine, string eventType, string timestamp)
    {
        using var hmac = new HMACSHA256(_hmacKey);
        var payload = $"{machine}|{eventType}|{timestamp}";
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }

    public void Dispose()
    {
    }
}
