using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Infrastructure.Migrations
{
    /// <summary>Replaces the Users.Role enum column with roles, the permission catalog and role grants. The three system
    /// roles are created here with fixed ids (1 Administrator, 2 Manager, 3 Employee) so existing users can be moved
    /// onto them; the permissions and their grants are seeded at startup by AccessControlSeeder.</summary>
    public partial class AddRolesAndPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Group = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsAssignable = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE"),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "INTEGER", nullable: false),
                    PermissionId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Key",
                table: "Permissions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Name", "IsSystem", "CreatedAt" },
                values: new object[,]
                {
                    { 1, "Administrator", true, new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "Manager", true, new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, "Employee", true, new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc) }
                });

            // Move existing users onto the system roles, then drop the old enum column.
            migrationBuilder.AddColumn<int>(
                name: "RoleId",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.Sql(
                "UPDATE \"Users\" SET \"RoleId\" = CASE \"Role\" WHEN 'Administrator' THEN 1 WHEN 'Manager' THEN 2 ELSE 3 END;");

            // The claim shape changed: end every session so everyone signs in again on the new model.
            migrationBuilder.Sql(
                "UPDATE \"Users\" SET \"SecurityStamp\" = lower(hex(randomblob(16)));");

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");

            // System roles and the permission catalog are read-only for everyone. Rows may still be inserted, which is how
            // the startup seeder adds permissions and grants them to the Administrator.
            migrationBuilder.Sql(
                "CREATE TRIGGER \"TR_Roles_SystemNoUpdate\" BEFORE UPDATE ON \"Roles\" WHEN OLD.\"IsSystem\" = 1 "
                + "BEGIN SELECT RAISE(ABORT, 'System roles are read-only.'); END;");
            migrationBuilder.Sql(
                "CREATE TRIGGER \"TR_Roles_SystemNoDelete\" BEFORE DELETE ON \"Roles\" WHEN OLD.\"IsSystem\" = 1 "
                + "BEGIN SELECT RAISE(ABORT, 'System roles cannot be deleted.'); END;");
            migrationBuilder.Sql(
                "CREATE TRIGGER \"TR_Permissions_NoUpdate\" BEFORE UPDATE ON \"Permissions\" "
                + "BEGIN SELECT RAISE(ABORT, 'The permission catalog is read-only.'); END;");
            migrationBuilder.Sql(
                "CREATE TRIGGER \"TR_Permissions_NoDelete\" BEFORE DELETE ON \"Permissions\" "
                + "BEGIN SELECT RAISE(ABORT, 'The permission catalog is read-only.'); END;");
            migrationBuilder.Sql(
                "CREATE TRIGGER \"TR_RolePermissions_SystemNoUpdate\" BEFORE UPDATE ON \"RolePermissions\" "
                + "WHEN (SELECT \"IsSystem\" FROM \"Roles\" WHERE \"Id\" = OLD.\"RoleId\") = 1 "
                + "BEGIN SELECT RAISE(ABORT, 'System roles are read-only.'); END;");
            migrationBuilder.Sql(
                "CREATE TRIGGER \"TR_RolePermissions_SystemNoDelete\" BEFORE DELETE ON \"RolePermissions\" "
                + "WHEN (SELECT \"IsSystem\" FROM \"Roles\" WHERE \"Id\" = OLD.\"RoleId\") = 1 "
                + "BEGIN SELECT RAISE(ABORT, 'System roles are read-only.'); END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"TR_RolePermissions_SystemNoDelete\";");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"TR_RolePermissions_SystemNoUpdate\";");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"TR_Permissions_NoDelete\";");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"TR_Permissions_NoUpdate\";");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"TR_Roles_SystemNoDelete\";");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS \"TR_Roles_SystemNoUpdate\";");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "Employee");

            // Custom roles have no equivalent in the old model; those users become Employees.
            migrationBuilder.Sql(
                "UPDATE \"Users\" SET \"Role\" = CASE \"RoleId\" WHEN 1 THEN 'Administrator' WHEN 2 THEN 'Manager' ELSE 'Employee' END;");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Users_RoleId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "Users");
        }
    }
}
