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
    public DbSet<LegalEntityProfile> LegalEntityProfiles => Set<LegalEntityProfile>();

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
            entity.Property(u => u.EmailConfirmationCode).HasColumnName("EmailConfirmationCode");
            entity.Property(u => u.EmailConfirmationCodeExpiresAt).HasColumnName("EmailConfirmationCodeExpiresAt");
            entity.Property(u => u.EmailCodeVerifiedAt).HasColumnName("EmailCodeVerifiedAt");
            entity.Property(u => u.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(u => u.AccountType).HasColumnName("AccountType").HasMaxLength(20);

            entity.HasIndex(u => u.Email).IsUnique();

            entity.HasOne(u => u.LegalEntityProfile)
                .WithOne(p => p.User)
                .HasForeignKey<LegalEntityProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LegalEntityProfile>(entity =>
        {
            entity.ToTable("LegalEntityProfiles");
            entity.HasKey(p => p.UserId);

            entity.Property(p => p.UserId).HasColumnName("UserId");
            entity.Property(p => p.CompanyFullName).HasColumnName("CompanyFullName").HasMaxLength(500);
            entity.Property(p => p.CompanyShortName).HasColumnName("CompanyShortName").HasMaxLength(160);
            entity.Property(p => p.Inn).HasColumnName("Inn").HasMaxLength(10);
            entity.Property(p => p.Ogrn).HasColumnName("Ogrn").HasMaxLength(13);
            entity.Property(p => p.Kpp).HasColumnName("Kpp").HasMaxLength(9);
            entity.Property(p => p.DirectorFullName).HasColumnName("DirectorFullName").HasMaxLength(200);
            entity.Property(p => p.DirectorBirthDate).HasColumnName("DirectorBirthDate");
            entity.Property(p => p.DocumentFileName).HasColumnName("DocumentFileName").HasMaxLength(255);
            entity.Property(p => p.DocumentContent).HasColumnName("DocumentContent");
            entity.Property(p => p.VerifiedAt).HasColumnName("VerifiedAt");
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
