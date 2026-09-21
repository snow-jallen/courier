using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Courier.Data;

/// <summary>Where the directory lives and how to open it. One file, one machine.</summary>
public static class CourierDatabase
{
    public static string DefaultFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Courier");

    public static string DefaultPath => Path.Combine(DefaultFolder, "contacts.db");

    public static DbContextOptions<CourierDbContext> Options(string path) =>
        new DbContextOptionsBuilder<CourierDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;

    public static CourierDbContext Open(string path)
    {
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        return new CourierDbContext(Options(path));
    }

    /// <summary>Timestamped copy of the database file, taken before every import.</summary>
    public static string BackUp(string path, DateTimeOffset now)
    {
        var folder = Path.Combine(Path.GetDirectoryName(path) ?? ".", "backups");
        Directory.CreateDirectory(folder);
        var stamp = now.ToLocalTime().ToString("yyyyMMdd-HHmmss");
        var destination = Path.Combine(folder, $"contacts-{stamp}.db");
        File.Copy(path, destination, overwrite: true);
        return destination;
    }
}

/// <summary>Lets `dotnet ef` build the context without an application host.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<CourierDbContext>
{
    public CourierDbContext CreateDbContext(string[] args) =>
        new(CourierDatabase.Options("design-time.db"));
}
