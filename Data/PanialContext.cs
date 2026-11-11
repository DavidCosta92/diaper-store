using Microsoft.EntityFrameworkCore;
using panialera.Models;

namespace panialera.Data;

public class PanialContext : DbContext
{
    public PanialContext(DbContextOptions<PanialContext> options) : base(options) { }

    public DbSet<Product> Products { get; set; }
    public DbSet<Sale> Sales { get; set; }
    public DbSet<SaleItem> SaleItems { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<ProductPriceHistory> ProductPriceHistories { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<TodoItem> TodoItems { get; set; }
    public DbSet<Expense> Expenses { get; set; }
    public DbSet<DistributorProduct> DistributorProducts { get; set; }
    public DbSet<DistributorSync> DistributorSyncs { get; set; }
    public DbSet<StockCount> StockCounts { get; set; }
    public DbSet<StockCountItem> StockCountItems { get; set; }
    public DbSet<RemoteEventImport> RemoteEventImports { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SaleItem>()
            .HasOne(si => si.Sale)
            .WithMany(s => s.Items)
            .HasForeignKey(si => si.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Sale>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // A sale may have a customer; a customer with sales cannot be deleted
        // (Restrict here plus a check in the service).
        modelBuilder.Entity<Sale>()
            .HasOne(s => s.Customer)
            .WithMany()
            .HasForeignKey(s => s.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // A to-do may have a customer; deleting the customer leaves the to-do without one
        // (SetNull) instead of blocking the delete.
        modelBuilder.Entity<TodoItem>()
            .HasOne(t => t.Customer)
            .WithMany()
            .HasForeignKey(t => t.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        // Audit log: category stored as text; indexes for searching by date and by record.
        // No FKs on purpose: the log outlives the deleted product or user.
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.Property(a => a.Category).HasConversion<string>();
            e.HasIndex(a => a.Date);
            e.HasIndex(a => new { a.EntityType, a.EntityId });
        });

        modelBuilder.Entity<ProductPriceHistory>()
            .HasOne(pph => pph.Product)
            .WithMany()
            .HasForeignKey(pph => pph.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Product>()
            .HasOne(p => p.LastUpdatedByUser)
            .WithMany()
            .HasForeignKey(p => p.LastUpdatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // The code is not unique on purpose (it is external), but it is searched to match
        // the shop's products with the distributor's catalog.
        modelBuilder.Entity<Product>().HasIndex(p => p.Code);
        modelBuilder.Entity<DistributorProduct>().HasIndex(d => d.Code);

        // Stock counts: the rows are deleted with their count (when discarding one in progress).
        // ProductId has no FK on purpose: it is a snapshot, like sales.
        modelBuilder.Entity<StockCountItem>()
            .HasOne(i => i.StockCount)
            .WithMany(a => a.Items)
            .HasForeignKey(i => i.StockCountId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<StockCount>().Ignore(a => a.IsInProgress);
        modelBuilder.Entity<StockCountItem>().Ignore(i => i.Counted);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Name)
            .IsUnique();

        modelBuilder.Entity<RemoteEventImport>().HasKey(r => r.EventId);
    }
}
