using JustFlip.Models;
using Microsoft.EntityFrameworkCore;

namespace JustFlip;

public class JustFlipDbContext : DbContext
{
    // Kailangan lang natin ang constructor na ito para sa DI (Dependency Injection) ng Web Application
    public JustFlipDbContext(DbContextOptions<JustFlipDbContext> options)
        : base(options)
    {
    }

    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserLogin> UserLogins => Set<UserLogin>();
    public DbSet<UserOTP> UserOtps => Set<UserOTP>();
    public DbSet<DailySalesDepositRecord> DailySalesDepositRecords => Set<DailySalesDepositRecord>();
    public DbSet<DailySalesDepositRecordRow> DailySalesDepositRecordRows => Set<DailySalesDepositRecordRow>();
    public DbSet<EndDayReport> EndDayReports => Set<EndDayReport>();
    public DbSet<EndDayReportRow> EndDayReportRows => Set<EndDayReportRow>();
    public DbSet<DeletedDailySalesDepositRecordRow> DeletedDailySalesDepositRecordRows { get; set; }
    public DbSet<DeletedDailySalesDepositRecord> DeletedDailySalesDepositRecords { get; set; }
    public DbSet<DeletedEndDayReportRow> DeletedEndDayReportRows { get; set; }
    public DbSet<DeletedEndDayReport> DeletedEndDayReports { get; set; }
    public DbSet<AuditLogs> AuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();

        modelBuilder.Entity<DailySalesDepositRecordRow>()
        .HasIndex(r => r.DateOfTransaction);
    }
}