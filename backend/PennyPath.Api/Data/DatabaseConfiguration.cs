using Npgsql;

namespace PennyPath.Api.Data;

public static class DatabaseConfiguration
{
    public static string CreateConnectionString(IConfiguration configuration)
    {
        var database = configuration.GetSection("Database");
        var host = Required(database, "Host");
        var name = Required(database, "Name");
        var username = Required(database, "Username");
        var passwordFile = Required(database, "PasswordFile");
        if (!int.TryParse(Required(database, "Port"), out var port) || port is < 1 or > 65535)
        {
            throw new InvalidOperationException("Database:Port must be between 1 and 65535.");
        }

        string password;
        try
        {
            // Match a file secret ending in LF/CRLF without trimming password spaces.
            password = File.ReadAllText(passwordFile).TrimEnd('\r', '\n');
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException("The database password file could not be read.");
        }

        if (password.Length == 0)
        {
            throw new InvalidOperationException("The database password file must not be empty.");
        }

        return new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = name,
            Username = username,
            Password = password,
            IncludeErrorDetail = false
        }.ConnectionString;
    }

    private static string Required(IConfigurationSection database, string key)
    {
        var value = database[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Database:{key} must be configured.");
        }

        return value;
    }
}
