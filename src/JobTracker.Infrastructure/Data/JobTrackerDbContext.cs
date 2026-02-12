using JobTracker.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Infrastructure.Data;

public class JobTrackerDbContext : DbContext
{
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<Email> Emails => Set<Email>();
    public DbSet<EmailAccount> EmailAccounts => Set<EmailAccount>();

    public JobTrackerDbContext(DbContextOptions<JobTrackerDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Application>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.CompanyName).IsRequired().HasMaxLength(200);
            entity.Property(a => a.JobTitle).IsRequired().HasMaxLength(200);
            entity.Property(a => a.Status).HasConversion<string>().HasMaxLength(50);
            entity.Property(a => a.Notes).HasMaxLength(2000);
            entity.HasMany(a => a.RelatedEmails)
                  .WithOne(e => e.Application)
                  .HasForeignKey(e => e.ApplicationId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Email>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Subject).IsRequired().HasMaxLength(500);
            entity.Property(e => e.From).IsRequired().HasMaxLength(300);
            entity.Property(e => e.To).IsRequired().HasMaxLength(300);
            entity.Property(e => e.ThreadId).HasMaxLength(200);
            entity.HasIndex(e => e.ApplicationId);
            entity.HasIndex(e => e.ReceivedDate);
        });

        modelBuilder.Entity<EmailAccount>(entity =>
        {
            entity.HasKey(ea => ea.Id);
            entity.Property(ea => ea.EmailAddress).IsRequired().HasMaxLength(300);
            entity.Property(ea => ea.ImapServer).IsRequired().HasMaxLength(200);
            entity.Property(ea => ea.Username).IsRequired().HasMaxLength(200);
            entity.Property(ea => ea.EncryptedPassword).IsRequired(false);
            entity.Property(ea => ea.AuthType).HasConversion<string>().HasMaxLength(50).HasDefaultValue(Core.Enums.EmailAuthType.Password);
            entity.Property(ea => ea.EncryptedRefreshToken).IsRequired(false);
            entity.Property(ea => ea.AccessToken).IsRequired(false);
        });
    }
}
