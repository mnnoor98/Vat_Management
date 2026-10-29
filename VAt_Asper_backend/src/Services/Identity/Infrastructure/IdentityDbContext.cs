using Microsoft.EntityFrameworkCore;
using Vat.Services.Identity.Domain;

namespace Vat.Services.Identity.Infrastructure;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<IdentityRole> Roles => Set<IdentityRole>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<IdentityUser>(b =>
        {
            b.ToTable("users", "identity", t =>
            {
                t.HasCheckConstraint("ck_users_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.Issuer).HasMaxLength(500).IsRequired();
            b.Property(x => x.Subject).HasMaxLength(200).IsRequired();
            b.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Issuer, x.Subject }).IsUnique().AreNullsDistinct(false);
        });
        model.Entity<IdentityRole>(b =>
        {
            b.ToTable("roles", "identity", t =>
            {
                t.HasCheckConstraint("ck_roles_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.Code).HasMaxLength(100).IsRequired();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Code }).IsUnique().AreNullsDistinct(false);
        });
        model.Entity<IdentityPermission>(b =>
        {
            b.ToTable("permissions", "identity", t =>
            {
                t.HasCheckConstraint("ck_permissions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.Code).HasMaxLength(200).IsRequired();
            b.Property(x => x.Description).HasMaxLength(500).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Code }).IsUnique().AreNullsDistinct(false);
        });
        model.Entity<UserRole>(b =>
        {
            b.ToTable("user_roles", "identity", t =>
            {
                t.HasCheckConstraint("ck_user_roles_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.HasOne<IdentityUser>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.UserId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.UserId });
            b.HasOne<IdentityRole>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.RoleId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.RoleId });
            // BranchId belongs to another service; scope validation is an application contract.
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.UserId, x.RoleId, x.BranchId }).IsUnique().AreNullsDistinct(false);
        });
        model.Entity<RolePermission>(b =>
        {
            b.ToTable("role_permissions", "identity", t =>
            {
                t.HasCheckConstraint("ck_role_permissions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.HasOne<IdentityRole>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.RoleId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.RoleId });
            b.HasOne<IdentityPermission>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.PermissionId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.PermissionId });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.RoleId, x.PermissionId }).IsUnique().AreNullsDistinct(false);
        });
        model.Entity<MenuItem>(b =>
        {
            b.ToTable("menu_items", "identity", t =>
            {
                t.HasCheckConstraint("ck_menu_items_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                t.HasCheckConstraint("ck_menu_items_rules", "(\"ParentId\" IS NULL OR \"ParentId\" <> \"Id\")");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.Property(x => x.Code).HasMaxLength(100).IsRequired();
            b.Property(x => x.Label).HasMaxLength(200).IsRequired();
            b.Property(x => x.Route).HasMaxLength(500).IsRequired();
            b.HasOne<MenuItem>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ParentId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ParentId });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.Code }).IsUnique().AreNullsDistinct(false);
        });
        model.Entity<RoleMenu>(b =>
        {
            b.ToTable("role_menus", "identity", t =>
            {
                t.HasCheckConstraint("ck_role_menus_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.HasOne<IdentityRole>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.RoleId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.RoleId });
            b.HasOne<MenuItem>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.MenuId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.MenuId });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.RoleId, x.MenuId }).IsUnique().AreNullsDistinct(false);
        });
        model.Entity<RefreshSession>(b =>
        {
            b.ToTable("refresh_sessions", "identity", t =>
            {
                t.HasCheckConstraint("ck_refresh_sessions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                t.HasCheckConstraint("ck_refresh_sessions_rules", "(\"ExpiresAt\" > \"CreatedAt\" AND (\"RevokedAt\" IS NULL OR \"RevokedAt\" >= \"CreatedAt\") AND (\"ReplacedById\" IS NULL OR \"ReplacedById\" <> \"Id\"))");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.HasOne<IdentityUser>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.UserId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.UserId });
            b.Property(x => x.TokenDigest).HasMaxLength(128).IsRequired();
            b.HasOne<RefreshSession>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.ReplacedById })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.ReplacedById });
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.TokenDigest }).IsUnique().AreNullsDistinct(false);
        });
        model.Entity<SecurityAudit>(b =>
        {
            b.ToTable("security_audit", "identity", t =>
            {
                t.HasCheckConstraint("ck_security_audit_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
            });
            b.HasKey(x => new { x.TenantId, x.CompanyId, x.Id });
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            b.Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
            b.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.CreatedAt });
            b.HasOne<IdentityUser>().WithMany().HasForeignKey(x => new { x.TenantId, x.CompanyId, x.UserId })
                .HasPrincipalKey(x => new { x.TenantId, x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.CompanyId, x.UserId });
            b.Property(x => x.Action).HasMaxLength(100).IsRequired();
            b.Property(x => x.Resource).HasMaxLength(500).IsRequired();
            b.Property(x => x.CorrelationId).HasMaxLength(100).IsRequired();
            b.Property(x => x.Outcome).HasMaxLength(40).IsRequired();
        });
    }
}
