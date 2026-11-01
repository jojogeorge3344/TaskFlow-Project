using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Api.Persistence.Migrations
{
    /// <summary>Providers become data: each AiProviderConfigs row is a provider with a name, an API type, Local or Cloud
    /// and a built-in flag, and models and chat messages point at it by id instead of by kind. The existing rows become the
    /// built-in providers, keeping their keys, saved model and enabled flag; models and messages are mapped onto them
    /// before the old kind columns are dropped. Down is lossy for custom providers only (they and their models go).</summary>
    public partial class AddCustomAiProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AiProviderConfigs_Kind",
                table: "AiProviderConfigs");

            migrationBuilder.RenameColumn(
                name: "Kind",
                table: "AiProviderConfigs",
                newName: "ApiType");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "AiProviderConfigs",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "",
                collation: "NOCASE");

            migrationBuilder.AddColumn<string>(
                name: "Locality",
                table: "AiProviderConfigs",
                type: "TEXT",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsBuiltIn",
                table: "AiProviderConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ProviderId",
                table: "AiModels",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProviderId",
                table: "AssistantMessages",
                type: "INTEGER",
                nullable: true);

            // The existing rows (one per kind) become the built-in providers.
            migrationBuilder.Sql("""
                UPDATE "AiProviderConfigs" SET
                    "IsBuiltIn" = 1,
                    "Name" = CASE "ApiType" WHEN 'Ollama' THEN 'Ollama' WHEN 'Gemini' THEN 'Google Gemini' ELSE 'OpenAI GPT' END,
                    "Locality" = CASE "ApiType" WHEN 'Ollama' THEN 'Local' ELSE 'Cloud' END;
                """);

            // A kind that only had models (no saved settings yet) gets its built-in row now, so every model maps onto one.
            migrationBuilder.Sql("""
                INSERT INTO "AiProviderConfigs" ("ApiType", "Name", "Locality", "IsBuiltIn", "IsEnabled", "ModelName", "BaseUrl", "UpdatedAt")
                SELECT kinds."Kind",
                       CASE kinds."Kind" WHEN 'Ollama' THEN 'Ollama' WHEN 'Gemini' THEN 'Google Gemini' ELSE 'OpenAI GPT' END,
                       CASE kinds."Kind" WHEN 'Ollama' THEN 'Local' ELSE 'Cloud' END,
                       1, 0,
                       CASE kinds."Kind" WHEN 'Ollama' THEN 'llama3.2:3b' WHEN 'Gemini' THEN 'gemini-3.8-flash' ELSE 'gpt-4o-mini' END,
                       CASE kinds."Kind" WHEN 'Ollama' THEN 'http://localhost:11434' ELSE NULL END,
                       strftime('%Y-%m-%d %H:%M:%f', 'now')
                FROM (SELECT DISTINCT "Kind" FROM "AiModels") AS kinds
                WHERE NOT EXISTS (SELECT 1 FROM "AiProviderConfigs" c WHERE c."ApiType" = kinds."Kind");
                """);

            migrationBuilder.Sql("""
                UPDATE "AiModels"
                SET "ProviderId" = (SELECT c."Id" FROM "AiProviderConfigs" c WHERE c."ApiType" = "AiModels"."Kind");
                """);

            migrationBuilder.Sql("""
                UPDATE "AssistantMessages"
                SET "ProviderId" = (SELECT c."Id" FROM "AiProviderConfigs" c WHERE c."ApiType" = "AssistantMessages"."ProviderKind")
                WHERE "ProviderKind" IS NOT NULL;
                """);

            migrationBuilder.DropIndex(
                name: "IX_AiModels_Kind_Name",
                table: "AiModels");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "AiModels");

            migrationBuilder.DropColumn(
                name: "ProviderKind",
                table: "AssistantMessages");

            migrationBuilder.CreateIndex(
                name: "IX_AiProviderConfigs_Name",
                table: "AiProviderConfigs",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiModels_ProviderId_Name",
                table: "AiModels",
                columns: new[] { "ProviderId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantMessages_ProviderId",
                table: "AssistantMessages",
                column: "ProviderId");

            migrationBuilder.AddForeignKey(
                name: "FK_AiModels_AiProviderConfigs_ProviderId",
                table: "AiModels",
                column: "ProviderId",
                principalTable: "AiProviderConfigs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantMessages_AiProviderConfigs_ProviderId",
                table: "AssistantMessages",
                column: "ProviderId",
                principalTable: "AiProviderConfigs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiModels_AiProviderConfigs_ProviderId",
                table: "AiModels");

            migrationBuilder.DropForeignKey(
                name: "FK_AssistantMessages_AiProviderConfigs_ProviderId",
                table: "AssistantMessages");

            migrationBuilder.DropIndex(
                name: "IX_AssistantMessages_ProviderId",
                table: "AssistantMessages");

            migrationBuilder.DropIndex(
                name: "IX_AiProviderConfigs_Name",
                table: "AiProviderConfigs");

            migrationBuilder.DropIndex(
                name: "IX_AiModels_ProviderId_Name",
                table: "AiModels");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "AiModels",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProviderKind",
                table: "AssistantMessages",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            // Custom providers cannot be expressed by kind: they and their models go; their messages keep no provider.
            migrationBuilder.Sql("""
                UPDATE "AssistantMessages"
                SET "ProviderKind" = (SELECT c."ApiType" FROM "AiProviderConfigs" c WHERE c."Id" = "AssistantMessages"."ProviderId" AND c."IsBuiltIn" = 1);
                DELETE FROM "AiModels" WHERE "ProviderId" IN (SELECT "Id" FROM "AiProviderConfigs" WHERE "IsBuiltIn" = 0);
                UPDATE "AiModels"
                SET "Kind" = (SELECT c."ApiType" FROM "AiProviderConfigs" c WHERE c."Id" = "AiModels"."ProviderId");
                DELETE FROM "AiProviderConfigs" WHERE "IsBuiltIn" = 0;
                """);

            migrationBuilder.DropColumn(
                name: "ProviderId",
                table: "AssistantMessages");

            migrationBuilder.DropColumn(
                name: "ProviderId",
                table: "AiModels");

            migrationBuilder.DropColumn(
                name: "IsBuiltIn",
                table: "AiProviderConfigs");

            migrationBuilder.DropColumn(
                name: "Locality",
                table: "AiProviderConfigs");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "AiProviderConfigs");

            migrationBuilder.RenameColumn(
                name: "ApiType",
                table: "AiProviderConfigs",
                newName: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_AiProviderConfigs_Kind",
                table: "AiProviderConfigs",
                column: "Kind",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiModels_Kind_Name",
                table: "AiModels",
                columns: new[] { "Kind", "Name" },
                unique: true);
        }
    }
}
