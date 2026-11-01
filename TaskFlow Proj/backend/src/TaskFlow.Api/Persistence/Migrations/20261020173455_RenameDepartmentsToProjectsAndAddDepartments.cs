using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameDepartmentsToProjectsAndAddDepartments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Departments_DepartmentId",
                table: "Tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Departments_DepartmentId",
                table: "Users");

            // The existing Department concept becomes Project: rename in place so no rows are lost.
            migrationBuilder.RenameTable(
                name: "Departments",
                newName: "Projects");

            migrationBuilder.RenameTable(
                name: "ManagerDepartments",
                newName: "ManagerProjects");

            migrationBuilder.RenameColumn(
                name: "DepartmentId",
                table: "ManagerProjects",
                newName: "ProjectId");

            migrationBuilder.RenameColumn(
                name: "DepartmentId",
                table: "Tasks",
                newName: "ProjectId");

            migrationBuilder.RenameColumn(
                name: "DepartmentId",
                table: "Users",
                newName: "ProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_Departments_Name",
                table: "Projects",
                newName: "IX_Projects_Name");

            migrationBuilder.RenameIndex(
                name: "IX_ManagerDepartments_DepartmentId",
                table: "ManagerProjects",
                newName: "IX_ManagerProjects_ProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_Tasks_DepartmentId",
                table: "Tasks",
                newName: "IX_Tasks_ProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_Users_DepartmentId",
                table: "Users",
                newName: "IX_Users_ProjectId");

            // The new Department feature reclaims the freed Departments name and adds its membership tables.
            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DepartmentManagers",
                columns: table => new
                {
                    ManagerUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    DepartmentId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentManagers", x => new { x.ManagerUserId, x.DepartmentId });
                    table.ForeignKey(
                        name: "FK_DepartmentManagers_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DepartmentManagers_Users_ManagerUserId",
                        column: x => x.ManagerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "Users",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Name",
                table: "Departments",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentManagers_DepartmentId",
                table: "DepartmentManagers",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DepartmentId",
                table: "Users",
                column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Projects_ProjectId",
                table: "Tasks",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Projects_ProjectId",
                table: "Users",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Departments_DepartmentId",
                table: "Users",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // TR_Permissions_NoUpdate blocks the seeder from touching existing rows, so the
            // departments.* keys keep their old project wording on databases created before this
            // change. Refresh them here, between dropping and recreating the trigger.
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS \"TR_Permissions_NoUpdate\";");
            migrationBuilder.Sql(
                "UPDATE \"Permissions\" SET \"Name\" = 'View departments', \"Description\" = 'See departments and the managers and employees in them.', \"Group\" = 'Departments' WHERE \"Key\" = 'departments.view';");
            migrationBuilder.Sql(
                "UPDATE \"Permissions\" SET \"Name\" = 'Manage departments', \"Description\" = 'Create, rename and delete departments and manage their members.', \"Group\" = 'Departments' WHERE \"Key\" = 'departments.manage';");
            migrationBuilder.Sql(
                "CREATE TRIGGER \"TR_Permissions_NoUpdate\" BEFORE UPDATE ON \"Permissions\" "
                + "BEGIN SELECT RAISE(ABORT, 'The permission catalog is read-only.'); END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS \"TR_Permissions_NoUpdate\";");
            migrationBuilder.Sql(
                "UPDATE \"Permissions\" SET \"Name\" = 'View projects', \"Description\" = 'See projects and the employees in them.', \"Group\" = 'Projects' WHERE \"Key\" = 'departments.view';");
            migrationBuilder.Sql(
                "UPDATE \"Permissions\" SET \"Name\" = 'Manage projects', \"Description\" = 'Create, rename and delete projects.', \"Group\" = 'Projects' WHERE \"Key\" = 'departments.manage';");
            migrationBuilder.Sql(
                "CREATE TRIGGER \"TR_Permissions_NoUpdate\" BEFORE UPDATE ON \"Permissions\" "
                + "BEGIN SELECT RAISE(ABORT, 'The permission catalog is read-only.'); END;");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Departments_DepartmentId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "DepartmentManagers");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Users_DepartmentId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Projects_ProjectId",
                table: "Tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Projects_ProjectId",
                table: "Users");

            migrationBuilder.RenameTable(
                name: "ManagerProjects",
                newName: "ManagerDepartments");

            migrationBuilder.RenameColumn(
                name: "ProjectId",
                table: "ManagerDepartments",
                newName: "DepartmentId");

            migrationBuilder.RenameTable(
                name: "Projects",
                newName: "Departments");

            migrationBuilder.RenameIndex(
                name: "IX_ManagerProjects_ProjectId",
                table: "ManagerDepartments",
                newName: "IX_ManagerDepartments_DepartmentId");

            migrationBuilder.RenameIndex(
                name: "IX_Projects_Name",
                table: "Departments",
                newName: "IX_Departments_Name");

            migrationBuilder.RenameColumn(
                name: "ProjectId",
                table: "Users",
                newName: "DepartmentId");

            migrationBuilder.RenameColumn(
                name: "ProjectId",
                table: "Tasks",
                newName: "DepartmentId");

            migrationBuilder.RenameIndex(
                name: "IX_Users_ProjectId",
                table: "Users",
                newName: "IX_Users_DepartmentId");

            migrationBuilder.RenameIndex(
                name: "IX_Tasks_ProjectId",
                table: "Tasks",
                newName: "IX_Tasks_DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Departments_DepartmentId",
                table: "Users",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Departments_DepartmentId",
                table: "Tasks",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
