using Microsoft.EntityFrameworkCore;
using MyApp.Models;

namespace MyApp.Data;

public class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<News> News => Set<News>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<BalanceTransaction> BalanceTransactions => Set<BalanceTransaction>();

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            // Явное маппирование под таблицы в PostgreSQL
            entity.ToTable("Users");

            entity.Property(u => u.Id).HasColumnName("Id");
            entity.Property(u => u.Email).HasColumnName("Email");
            entity.Property(u => u.PasswordHash).HasColumnName("PasswordHash");
            entity.Property(u => u.IsEmailConfirmed).HasColumnName("IsEmailConfirmed");
            entity.Property(u => u.EmailConfirmationToken).HasColumnName("EmailConfirmationToken");
            entity.Property(u => u.EmailConfirmationTokenExpiresAt).HasColumnName("EmailConfirmationTokenExpiresAt");
            entity.Property(u => u.CreatedAt).HasColumnName("CreatedAt");

            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<News>(entity =>
        {
            entity.ToTable("News");

            entity.Property(n => n.Id).HasColumnName("Id");
            entity.Property(n => n.Title).HasColumnName("Title");
            entity.Property(n => n.Content).HasColumnName("Content");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products");
            entity.Property(p => p.Id).HasColumnName("Id");
            entity.Property(p => p.Name).HasColumnName("Name");
            entity.Property(p => p.Description).HasColumnName("Description");
            entity.Property(p => p.PriceRub).HasColumnName("PriceRub").HasPrecision(14, 2);
        });

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("UserAccounts");
            entity.HasKey(a => a.UserId);

            entity.Property(a => a.UserId).HasColumnName("UserId");
            entity.Property(a => a.BalanceRub).HasColumnName("BalanceRub").HasPrecision(14, 2);
            entity.Property(a => a.DebtRub).HasColumnName("DebtRub").HasPrecision(14, 2);
            entity.Property(a => a.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasOne(a => a.User)
                .WithOne()
                .HasForeignKey<UserAccount>(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Contract>(entity =>
        {
            entity.ToTable("Contracts");
            entity.HasKey(c => c.Id);

            entity.Property(c => c.Id).HasColumnName("Id");
            entity.Property(c => c.UserId).HasColumnName("UserId");
            entity.Property(c => c.ProductId).HasColumnName("ProductId");
            entity.Property(c => c.ContractNumber).HasColumnName("ContractNumber");
            entity.Property(c => c.AmountRub).HasColumnName("AmountRub").HasPrecision(14, 2);
            entity.Property(c => c.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(c => c.EffectiveFrom).HasColumnName("EffectiveFrom");

            entity.HasIndex(c => c.ContractNumber).IsUnique();

            entity.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.Product)
                .WithMany()
                .HasForeignKey(c => c.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BalanceTransaction>(entity =>
        {
            entity.ToTable("BalanceTransactions");
            entity.HasKey(t => t.Id);

            entity.Property(t => t.Id).HasColumnName("Id");
            entity.Property(t => t.UserId).HasColumnName("UserId");
            entity.Property(t => t.Type).HasColumnName("Type");
            entity.Property(t => t.Description).HasColumnName("Description");
            entity.Property(t => t.AmountRub).HasColumnName("AmountRub").HasPrecision(14, 2);
            entity.Property(t => t.CreatedAt).HasColumnName("CreatedAt");

            entity.HasIndex(t => t.UserId);
            entity.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

    }
}
