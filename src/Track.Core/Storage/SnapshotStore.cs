using Microsoft.Data.Sqlite;
using Track.Core.Models;

namespace Track.Core.Storage;

/// <summary>Minimal SQLite history so pace charts can grow when APIs only return current totals.</summary>
public sealed class SnapshotStore : IDisposable
{
    private readonly string _connectionString;
    private readonly SqliteConnection _connection;

    public SnapshotStore(string? databasePath = null)
    {
        var path = databasePath ?? DefaultPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _connectionString = $"Data Source={path}";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();
        EnsureSchema();
    }

    public static string DefaultPath()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Track");
        return Path.Combine(root, "history.db");
    }

    public void AppendMeterSample(string providerId, string meterId, double used, double limit, DateTimeOffset at)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO meter_samples (provider_id, meter_id, used, limit_value, sampled_at)
            VALUES ($p, $m, $u, $l, $t);
            """;
        cmd.Parameters.AddWithValue("$p", providerId);
        cmd.Parameters.AddWithValue("$m", meterId);
        cmd.Parameters.AddWithValue("$u", used);
        cmd.Parameters.AddWithValue("$l", limit);
        cmd.Parameters.AddWithValue("$t", at.UtcDateTime.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<UsagePoint> GetSeries(string providerId, string meterId, DateTimeOffset since)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT sampled_at, used FROM meter_samples
            WHERE provider_id = $p AND meter_id = $m AND sampled_at >= $s
            ORDER BY sampled_at ASC;
            """;
        cmd.Parameters.AddWithValue("$p", providerId);
        cmd.Parameters.AddWithValue("$m", meterId);
        cmd.Parameters.AddWithValue("$s", since.UtcDateTime.ToString("O"));

        var points = new List<UsagePoint>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var at = DateTimeOffset.Parse(reader.GetString(0));
            var used = reader.GetDouble(1);
            points.Add(new UsagePoint(at, used));
        }

        return points;
    }

    public void Dispose() => _connection.Dispose();

    private void EnsureSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS meter_samples (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              provider_id TEXT NOT NULL,
              meter_id TEXT NOT NULL,
              used REAL NOT NULL,
              limit_value REAL NOT NULL,
              sampled_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_meter_samples_lookup
              ON meter_samples (provider_id, meter_id, sampled_at);
            """;
        cmd.ExecuteNonQuery();
    }
}
