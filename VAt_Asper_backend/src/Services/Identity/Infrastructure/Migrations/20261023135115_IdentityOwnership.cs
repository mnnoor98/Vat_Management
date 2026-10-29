using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vat.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IdentityOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "menu_items",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Route = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menu_items", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_menu_items_rules", "(\"ParentId\" IS NULL OR \"ParentId\" <> \"Id\")");
                    table.CheckConstraint("ck_menu_items_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_menu_items_menu_items_TenantId_CompanyId_ParentId",
                        columns: x => new { x.TenantId, x.CompanyId, x.ParentId },
                        principalSchema: "identity",
                        principalTable: "menu_items",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_permissions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_roles_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Issuer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Disabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_users_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                });

            migrationBuilder.CreateTable(
                name: "role_menus",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_menus", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_role_menus_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_role_menus_menu_items_TenantId_CompanyId_MenuId",
                        columns: x => new { x.TenantId, x.CompanyId, x.MenuId },
                        principalSchema: "identity",
                        principalTable: "menu_items",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_role_menus_roles_TenantId_CompanyId_RoleId",
                        columns: x => new { x.TenantId, x.CompanyId, x.RoleId },
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_role_permissions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_role_permissions_permissions_TenantId_CompanyId_PermissionId",
                        columns: x => new { x.TenantId, x.CompanyId, x.PermissionId },
                        principalSchema: "identity",
                        principalTable: "permissions",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_TenantId_CompanyId_RoleId",
                        columns: x => new { x.TenantId, x.CompanyId, x.RoleId },
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_sessions",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenDigest = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReplacedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_sessions", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_refresh_sessions_rules", "(\"ExpiresAt\" > \"CreatedAt\" AND (\"RevokedAt\" IS NULL OR \"RevokedAt\" >= \"CreatedAt\") AND (\"ReplacedById\" IS NULL OR \"ReplacedById\" <> \"Id\"))");
                    table.CheckConstraint("ck_refresh_sessions_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_refresh_sessions_refresh_sessions_TenantId_CompanyId_Replac~",
                        columns: x => new { x.TenantId, x.CompanyId, x.ReplacedById },
                        principalSchema: "identity",
                        principalTable: "refresh_sessions",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refresh_sessions_users_TenantId_CompanyId_UserId",
                        columns: x => new { x.TenantId, x.CompanyId, x.UserId },
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "security_audit",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Resource = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_audit", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_security_audit_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_security_audit_users_TenantId_CompanyId_UserId",
                        columns: x => new { x.TenantId, x.CompanyId, x.UserId },
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.TenantId, x.CompanyId, x.Id });
                    table.CheckConstraint("ck_user_roles_version", "\"Version\" > 0 AND \"UpdatedAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_user_roles_roles_TenantId_CompanyId_RoleId",
                        columns: x => new { x.TenantId, x.CompanyId, x.RoleId },
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_roles_users_TenantId_CompanyId_UserId",
                        columns: x => new { x.TenantId, x.CompanyId, x.UserId },
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_menu_items_TenantId_CompanyId_Code",
                schema: "identity",
                table: "menu_items",
                columns: new[] { "TenantId", "CompanyId", "Code" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_menu_items_TenantId_CompanyId_CreatedAt",
                schema: "identity",
                table: "menu_items",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_menu_items_TenantId_CompanyId_ParentId",
                schema: "identity",
                table: "menu_items",
                columns: new[] { "TenantId", "CompanyId", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_permissions_TenantId_CompanyId_Code",
                schema: "identity",
                table: "permissions",
                columns: new[] { "TenantId", "CompanyId", "Code" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_permissions_TenantId_CompanyId_CreatedAt",
                schema: "identity",
                table: "permissions",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_sessions_TenantId_CompanyId_CreatedAt",
                schema: "identity",
                table: "refresh_sessions",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_sessions_TenantId_CompanyId_ReplacedById",
                schema: "identity",
                table: "refresh_sessions",
                columns: new[] { "TenantId", "CompanyId", "ReplacedById" });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_sessions_TenantId_CompanyId_TokenDigest",
                schema: "identity",
                table: "refresh_sessions",
                columns: new[] { "TenantId", "CompanyId", "TokenDigest" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_sessions_TenantId_CompanyId_UserId",
                schema: "identity",
                table: "refresh_sessions",
                columns: new[] { "TenantId", "CompanyId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_role_menus_TenantId_CompanyId_CreatedAt",
                schema: "identity",
                table: "role_menus",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_role_menus_TenantId_CompanyId_MenuId",
                schema: "identity",
                table: "role_menus",
                columns: new[] { "TenantId", "CompanyId", "MenuId" });

            migrationBuilder.CreateIndex(
                name: "IX_role_menus_TenantId_CompanyId_RoleId",
                schema: "identity",
                table: "role_menus",
                columns: new[] { "TenantId", "CompanyId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_role_menus_TenantId_CompanyId_RoleId_MenuId",
                schema: "identity",
                table: "role_menus",
                columns: new[] { "TenantId", "CompanyId", "RoleId", "MenuId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_TenantId_CompanyId_CreatedAt",
                schema: "identity",
                table: "role_permissions",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_TenantId_CompanyId_PermissionId",
                schema: "identity",
                table: "role_permissions",
                columns: new[] { "TenantId", "CompanyId", "PermissionId" });

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_TenantId_CompanyId_RoleId",
                schema: "identity",
                table: "role_permissions",
                columns: new[] { "TenantId", "CompanyId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_role_permissions_TenantId_CompanyId_RoleId_PermissionId",
                schema: "identity",
                table: "role_permissions",
                columns: new[] { "TenantId", "CompanyId", "RoleId", "PermissionId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_roles_TenantId_CompanyId_Code",
                schema: "identity",
                table: "roles",
                columns: new[] { "TenantId", "CompanyId", "Code" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_roles_TenantId_CompanyId_CreatedAt",
                schema: "identity",
                table: "roles",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_security_audit_TenantId_CompanyId_CreatedAt",
                schema: "identity",
                table: "security_audit",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_security_audit_TenantId_CompanyId_UserId",
                schema: "identity",
                table: "security_audit",
                columns: new[] { "TenantId", "CompanyId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_TenantId_CompanyId_CreatedAt",
                schema: "identity",
                table: "user_roles",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_TenantId_CompanyId_RoleId",
                schema: "identity",
                table: "user_roles",
                columns: new[] { "TenantId", "CompanyId", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_TenantId_CompanyId_UserId",
                schema: "identity",
                table: "user_roles",
                columns: new[] { "TenantId", "CompanyId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_TenantId_CompanyId_UserId_RoleId_BranchId",
                schema: "identity",
                table: "user_roles",
                columns: new[] { "TenantId", "CompanyId", "UserId", "RoleId", "BranchId" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_users_TenantId_CompanyId_CreatedAt",
                schema: "identity",
                table: "users",
                columns: new[] { "TenantId", "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_users_TenantId_CompanyId_Issuer_Subject",
                schema: "identity",
                table: "users",
                columns: new[] { "TenantId", "CompanyId", "Issuer", "Subject" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refresh_sessions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "role_menus",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "role_permissions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "security_audit",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "menu_items",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "permissions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "users",
                schema: "identity");
        }
    }
}
