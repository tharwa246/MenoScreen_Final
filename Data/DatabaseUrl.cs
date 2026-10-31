using Npgsql;

namespace MenoScreen.Data;

// CONVERTS A DATABASE ADDRESS INTO THE FORMAT NPGSQL NEEDS
// Neon and Heroku both give us the database as a web-style address:
//     postgres://username:password@host:port/databasename
// Npgsql (the PostgreSQL driver) wants "Host=...;Username=...;Password=..." instead.
// This turns one into the other. If it's already in the Npgsql format, it's left alone.
public static class DatabaseUrl
{
    public static string ToNpgsqlConnectionString(string value)
    {
        if (!value.StartsWith("postgres://") && !value.StartsWith("postgresql://"))
            return value;

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
            // Always encrypt the connection: health data must never travel as plain text
            SslMode = SslMode.Require
        };
        return builder.ConnectionString;
    }
}