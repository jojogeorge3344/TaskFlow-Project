using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixDepartmentFollowUps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_Name",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Departments_Name",
                table: "Departments");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Name",
                table: "Projects",
                column: "Name",
                unique: true,
                filter: "\"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Name",
                table: "Departments",
                column: "Name",
                unique: true,
                filter: "\"IsDeleted\" = 0");

            // The previous migration reused the departments.* permission rows for the new Department feature, but
            // every role that held them held them for what are now projects. Give those roles the projects.* keys
            // too, so nobody loses project access. On a database that has never been seeded there is no
            // departments.* row to copy from, so nothing is inserted and the seeder applies its defaults instead.
            migrationBuilder.Sql(
                "INSERT INTO \"Permissions\" (\"Key\", \"Name\", \"Description\", \"Group\", \"IsAssignable\") "
                + "SELECT 'projects.view', 'View projects', 'See projects and the employees in them.', 'Projects', \"IsAssignable\" "
                + "FROM \"Permissions\" WHERE \"Key\" = 'departments.view' "
                + "AND NOT EXISTS (SELECT 1 FROM \"Permissions\" WHERE \"Key\" = 'projects.view');");
            migrationBuilder.Sql(
                "INSERT INTO \"Permissions\" (\"Key\", \"Name\", \"Description\", \"Group\", \"IsAssignable\") "
                + "SELECT 'projects.manage', 'Manage projects', 'Create, rename and delete projects.', 'Projects', \"IsAssignable\" "
                + "FROM \"Permissions\" WHERE \"Key\" = 'departments.manage' "
                + "AND NOT EXISTS (SELECT 1 FROM \"Permissions\" WHERE \"Key\" = 'projects.manage');");

            foreach (var (oldKey, newKey) in new[]
            {
                ("departments.view", "projects.view"),
                ("departments.manage", "projects.manage")
            })
            {
                migrationBuilder.Sql(
                    "INSERT INTO \"RolePermissions\" (\"RoleId\", \"PermissionId\") "
                    + "SELECT grants.\"RoleId\", target.\"Id\" "
                    + "FROM \"RolePermissions\" grants "
                    + $"JOIN \"Permissions\" source ON source.\"Id\" = grants.\"PermissionId\" AND source.\"Key\" = '{oldKey}' "
                    + $"JOIN \"Permissions\" target ON target.\"Key\" = '{newKey}' "
                    + "WHERE NOT EXISTS (SELECT 1 FROM \"RolePermissions\" existing "
                    + "WHERE existing.\"RoleId\" = grants.\"RoleId\" AND existing.\"PermissionId\" = target.\"Id\");");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_Name",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Departments_Name",
                table: "Departments");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Name",
                table: "Projects",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Name",
                table: "Departments",
                column: "Name",
                unique: true);
        }
    }
}
