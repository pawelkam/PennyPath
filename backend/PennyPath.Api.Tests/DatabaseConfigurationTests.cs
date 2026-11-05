using Microsoft.Extensions.Configuration;
using Npgsql;
using PennyPath.Api.Data;
using Xunit;

namespace PennyPath.Api.Tests;

[Trait("Category", "Persistence")]
public class DatabaseConfigurationTests : IDisposable
{
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("pennypath-db-config-");

    [Fact]
    public void PasswordFilePreservesSpacesAndEscapesConnectionStringCharacters()
    {
        const string password = " secret;Password=another;\"quoted\" ";
        File.WriteAllText(PasswordFile, password + "\r\n");
        var connection = new NpgsqlConnectionStringBuilder(DatabaseConfiguration.CreateConnectionString(Configuration()));

        Assert.Equal(password, connection.Password);
        Assert.Equal("postgres", connection.Host);
        Assert.Equal(5432, connection.Port);
        Assert.Equal("pennypath", connection.Database);
        Assert.Equal("pennypath", connection.Username);
        Assert.False(connection.IncludeErrorDetail);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("not-a-port")]
    public void InvalidPortIsRejected(string port)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DatabaseConfiguration.CreateConnectionString(Configuration(port: port)));
        Assert.Equal("Database:Port must be between 1 and 65535.", exception.Message);
    }

    [Theory]
    [InlineData("Host")]
    [InlineData("Port")]
    [InlineData("Name")]
    [InlineData("Username")]
    [InlineData("PasswordFile")]
    public void MissingConfigurationIsRejected(string key)
    {
        var configuration = Configuration();
        configuration[$"Database:{key}"] = "";
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DatabaseConfiguration.CreateConnectionString(configuration));
        Assert.Equal($"Database:{key} must be configured.", exception.Message);
    }

    [Fact]
    public void MissingSecretProducesASanitizedError()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DatabaseConfiguration.CreateConnectionString(Configuration()));
        Assert.Equal("The database password file could not be read.", exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\r\n")]
    public void EmptySecretIsRejected(string contents)
    {
        File.WriteAllText(PasswordFile, contents);
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DatabaseConfiguration.CreateConnectionString(Configuration()));
        Assert.Equal("The database password file must not be empty.", exception.Message);
    }

    [Fact]
    public void DesignTimeFactoryNeedsNoRuntimeConfigurationOrSecret()
    {
        using var database = new PennyPathIdentityDbContextFactory().CreateDbContext([]);
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", database.Database.ProviderName);
    }

    private string PasswordFile => Path.Combine(directory.FullName, "password");

    private IConfigurationRoot Configuration(string port = "5432") => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Host"] = "postgres",
            ["Database:Port"] = port,
            ["Database:Name"] = "pennypath",
            ["Database:Username"] = "pennypath",
            ["Database:PasswordFile"] = PasswordFile
        })
        .Build();

    public void Dispose()
    {
        directory.Delete(recursive: true);
    }
}
