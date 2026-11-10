using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Persistence.Read;

public sealed class InventoryReadDbContext : DbContext
{
    public InventoryReadDbContext(DbContextOptions<InventoryReadDbContext> options)
        : base(options)
    {
    }

    public DbSet<CategoryRecord> Categories => Set<CategoryRecord>();

    public DbSet<ProductRecord> Products => Set<ProductRecord>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var category = modelBuilder.Entity<CategoryRecord>();
        category.ToTable("Categories", "dbo");
        category.HasKey(record => record.Id);
        category.Property(record => record.Name).HasMaxLength(Category.NameMaxLength).IsRequired();
        category.Property(record => record.Description).HasMaxLength(Category.DescriptionMaxLength);
        MapProducts(modelBuilder);
    }

    private static void MapProducts(ModelBuilder modelBuilder)
    {
        var product = modelBuilder.Entity<ProductRecord>();
        product.ToTable("Products", "dbo");
        product.HasKey(record => record.Id);
        product.Property(record => record.Sku).HasMaxLength(ProductRules.SkuMaxLength).IsRequired();
        product.Property(record => record.Name).HasMaxLength(ProductRules.NameMaxLength).IsRequired();
        product.Property(record => record.Description).HasMaxLength(ProductRules.DescriptionMaxLength);
    }
}
