using Microsoft.EntityFrameworkCore;
using ReportingService.Models;

namespace ReportingService.Data;

public class ReportingDbContext : DbContext
{
    public ReportingDbContext(
        DbContextOptions<ReportingDbContext> options)
        : base(options)
    {
    }

    public DbSet<OrderReport> OrderReports => Set<OrderReport>();

    public DbSet<ProductReport> ProductReports => Set<ProductReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OrderReport>(entity =>
        {
            entity.ToTable("orders_reports");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id");

            entity.Property(x => x.OrderId)
                .HasColumnName("order_id");

            entity.Property(x => x.TotalRevenue)
                .HasColumnName("total_revenue")
                .HasPrecision(10, 2);

            entity.Property(x => x.TotalCost)
                .HasColumnName("total_cost")
                .HasPrecision(10, 2);

            entity.Property(x => x.TotalProfit)
                .HasColumnName("total_profit")
                .HasPrecision(10, 2);

            entity.HasIndex(x => x.OrderId).IsUnique();
        });

        modelBuilder.Entity<ProductReport>(entity =>
        {
            entity.ToTable("product_reports");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id");

            entity.Property(x => x.OrderReportId)
                .HasColumnName("order_report_id");

            entity.Property(x => x.ProductId)
                .HasColumnName("product_id");

            entity.Property(x => x.TotalSold)
                .HasColumnName("total_sold");

            entity.Property(x => x.Revenue)
                .HasColumnName("revenue")
                .HasPrecision(10, 2);

            entity.Property(x => x.Cost)
                .HasColumnName("cost")
                .HasPrecision(10, 2);

            entity.Property(x => x.Profit)
                .HasColumnName("profit")
                .HasPrecision(10, 2);

            entity.Property(x => x.StockQuantity)
                .HasColumnName("stock_quantity");

            entity.HasOne<OrderReport>()
                .WithMany()
                .HasForeignKey(x => x.OrderReportId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}