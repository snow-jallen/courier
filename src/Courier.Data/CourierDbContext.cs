using Courier.Core.Domain;
using Courier.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Courier.Data;

public class CourierDbContext(DbContextOptions<CourierDbContext> options) : DbContext(options)
{
    public DbSet<Person> People => Set<Person>();
    public DbSet<ContactPoint> ContactPoints => Set<ContactPoint>();
    public DbSet<ImportRun> ImportRuns => Set<ImportRun>();
    public DbSet<PersonChange> PersonChanges => Set<PersonChange>();
    public DbSet<MessageBatch> MessageBatches => Set<MessageBatch>();
    public DbSet<MessageDelivery> MessageDeliveries => Set<MessageDelivery>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // SQLite has no native date type. Storing instants as UTC text keeps ORDER BY
        // and WHERE working in SQL instead of forcing everything into memory first.
        builder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Every key is a Guid set in the entity's initializer. Saying so explicitly
        // matters: left as store-generated, EF reads an already-set key as proof the
        // row exists and issues an UPDATE where an INSERT was meant, so anything
        // attached through a navigation property silently fails to save.
        b.Entity<Person>(e =>
        {
            e.Property(p => p.Id).ValueGeneratedNever();
            e.HasIndex(p => new { p.LastName, p.FirstName });
            e.HasIndex(p => p.IsActive);
            e.HasIndex(p => p.Ward);

            // Enums are stored by name so a future channel cannot renumber existing rows.
            e.Property(p => p.Source).HasConversion<string>().HasMaxLength(16);

            e.Property(p => p.PreferredChannel)
                .HasConversion(v => v.ToWire(), v => Channels.FromWire(v))
                .HasMaxLength(16);

            e.HasMany(p => p.ContactPoints)
                .WithOne(c => c.Person!)
                .HasForeignKey(c => c.PersonId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ContactPoint>(e =>
        {
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(c => c.Source).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(c => new { c.PersonId, c.Kind });

            // Drives the "to enter in LCR" report.
            e.HasIndex(c => new { c.Source, c.EnteredInLcrOn, c.LastSeenInLcrOn });

            // The same address should not land on a person twice.
            e.HasIndex(c => new { c.PersonId, c.Kind, c.Normalized })
                .IsUnique()
                .HasFilter("\"Normalized\" IS NOT NULL");

            e.Ignore(c => c.NeedsEnteringInLcr);
        });

        b.Entity<ImportRun>(e =>
        {
            e.Property(r => r.Id).ValueGeneratedNever();
            e.HasIndex(r => r.ImportedAt);
            e.HasIndex(r => r.Sha256);
            e.HasMany(r => r.Changes)
                .WithOne(c => c.ImportRun!)
                .HasForeignKey(c => c.ImportRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PersonChange>(e =>
        {
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.Kind).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(c => c.PersonId);
            e.HasOne(c => c.Person)
                .WithMany()
                .HasForeignKey(c => c.PersonId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<MessageBatch>(e =>
        {
            e.Property(m => m.Id).ValueGeneratedNever();
            e.HasIndex(m => m.CreatedAt);
            e.HasMany(m => m.Deliveries)
                .WithOne(d => d.MessageBatch!)
                .HasForeignKey(d => d.MessageBatchId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<MessageDelivery>(e =>
        {
            e.Property(d => d.Id).ValueGeneratedNever();
            e.Property(d => d.Channel)
                .HasConversion(v => v.ToWire(), v => Channels.FromWire(v))
                .HasMaxLength(16);
            e.Property(d => d.Status).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(d => new { d.MessageBatchId, d.Status });
            e.HasIndex(d => d.PersonId);
            e.HasOne(d => d.Person)
                .WithMany()
                .HasForeignKey(d => d.PersonId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

internal sealed class UtcDateTimeConverter()
    : ValueConverter<DateTimeOffset, DateTime>(
        v => v.UtcDateTime,
        v => new DateTimeOffset(DateTime.SpecifyKind(v, DateTimeKind.Utc), TimeSpan.Zero));
