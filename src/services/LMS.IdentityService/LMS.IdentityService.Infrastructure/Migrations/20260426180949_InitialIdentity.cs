using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.IdentityService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "tenant_configs",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    LogoUrl = table.Column<string>(type: "text", nullable: true),
                    Timezone = table.Column<string>(type: "text", nullable: false, defaultValue: "Asia/Ho_Chi_Minh"),
                    AllowedEmailDomains = table.Column<string[]>(type: "text[]", nullable: false),
                    Plan = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_configs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_invites",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsAccepted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_invites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_profiles",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KeycloakId = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    AvatarUrl = table.Column<string>(type: "text", nullable: true),
                    Bio = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Timezone = table.Column<string>(type: "text", nullable: false, defaultValue: "Asia/Ho_Chi_Minh"),
                    Language = table.Column<string>(type: "text", nullable: false, defaultValue: "vi"),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_profiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_configs_TenantId_Id",
                schema: "identity",
                table: "tenant_configs",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_TenantConfig_TenantId",
                schema: "identity",
                table: "tenant_configs",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_invites_TenantId_Id",
                schema: "identity",
                table: "user_invites",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_UserInvite_Tenant_Email_Pending",
                schema: "identity",
                table: "user_invites",
                columns: new[] { "TenantId", "Email" },
                filter: "\"IsAccepted\" = false");

            migrationBuilder.CreateIndex(
                name: "UX_UserInvite_Token",
                schema: "identity",
                table: "user_invites",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_profiles_TenantId_Id",
                schema: "identity",
                table: "user_profiles",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_Tenant_Role",
                schema: "identity",
                table: "user_profiles",
                columns: new[] { "TenantId", "Role" });

            migrationBuilder.CreateIndex(
                name: "UX_UserProfile_Tenant_Email",
                schema: "identity",
                table: "user_profiles",
                columns: new[] { "TenantId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_UserProfile_Tenant_Keycloak",
                schema: "identity",
                table: "user_profiles",
                columns: new[] { "TenantId", "KeycloakId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_configs",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_invites",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_profiles",
                schema: "identity");
        }
    }
}
