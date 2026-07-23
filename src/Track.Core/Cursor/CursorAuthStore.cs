using Microsoft.Data.Sqlite;

namespace Track.Core.Cursor;

public sealed record CursorAuthSession(
    string AccessToken,
    string? RefreshToken,
    string? Email,
    string? MembershipType,
    string? SubscriptionStatus);

public static class CursorAuthStore
{
    public static string? FindStateDatabasePath()
    {
        var primary = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Cursor", "User", "globalStorage", "state.vscdb");
        if (File.Exists(primary)) return primary;

        var alt = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Cursor", "User", "globalStorage", "state.vscdb");
        return File.Exists(alt) ? alt : null;
    }

    public static bool IsCursorInstalled() =>
        FindStateDatabasePath() is not null
        || Directory.Exists(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Cursor"));

    public static CursorAuthSession? TryReadSession()
    {
        var dbPath = FindStateDatabasePath();
        if (dbPath is null) return null;

        try
        {
            return ReadFromDatabase(dbPath);
        }
        catch
        {
            // Cursor often holds a WAL lock — copy and read.
            var tempDir = Path.Combine(Path.GetTempPath(), "TrackCursorAuth");
            Directory.CreateDirectory(tempDir);
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                var src = dbPath + suffix;
                if (!File.Exists(src)) continue;
                File.Copy(src, Path.Combine(tempDir, "state.vscdb" + suffix), overwrite: true);
            }

            return ReadFromDatabase(Path.Combine(tempDir, "state.vscdb"));
        }
    }

    private static CursorAuthSession? ReadFromDatabase(string dbPath)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared
        }.ToString());
        connection.Open();

        string? Get(string key)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT value FROM ItemTable WHERE key = $k LIMIT 1;";
            cmd.Parameters.AddWithValue("$k", key);
            var result = cmd.ExecuteScalar();
            return result switch
            {
                null or DBNull => null,
                byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
                _ => Convert.ToString(result)
            };
        }

        var access = Get("cursorAuth/accessToken");
        if (string.IsNullOrWhiteSpace(access))
            return null;

        return new CursorAuthSession(
            AccessToken: access,
            RefreshToken: Get("cursorAuth/refreshToken"),
            Email: Get("cursorAuth/cachedEmail"),
            MembershipType: Get("cursorAuth/stripeMembershipType"),
            SubscriptionStatus: Get("cursorAuth/stripeSubscriptionStatus"));
    }
}
