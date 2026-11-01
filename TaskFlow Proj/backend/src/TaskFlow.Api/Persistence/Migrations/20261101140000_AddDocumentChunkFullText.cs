using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Api.Persistence.Migrations
{
    /// <summary>The FTS5 keyword index over the TFA document chunks (design H1 of tfa-hybrid-document-search). It reads its
    /// text from <c>DocumentChunks</c> (external content), triggers keep it in step with every insert, update and delete
    /// (including the cascade from a deleted source), and <c>rebuild</c> indexes the chunks already stored.</summary>
    public partial class AddDocumentChunkFullText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE VIRTUAL TABLE "DocumentChunks_fts" USING fts5(
                    "HeadingPath", "Text",
                    content='DocumentChunks', content_rowid='Id',
                    tokenize='porter unicode61');
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER "DocumentChunks_fts_ai" AFTER INSERT ON "DocumentChunks" BEGIN
                    INSERT INTO "DocumentChunks_fts"(rowid, "HeadingPath", "Text") VALUES (new."Id", new."HeadingPath", new."Text");
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER "DocumentChunks_fts_ad" AFTER DELETE ON "DocumentChunks" BEGIN
                    INSERT INTO "DocumentChunks_fts"("DocumentChunks_fts", rowid, "HeadingPath", "Text") VALUES ('delete', old."Id", old."HeadingPath", old."Text");
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER "DocumentChunks_fts_au" AFTER UPDATE ON "DocumentChunks" BEGIN
                    INSERT INTO "DocumentChunks_fts"("DocumentChunks_fts", rowid, "HeadingPath", "Text") VALUES ('delete', old."Id", old."HeadingPath", old."Text");
                    INSERT INTO "DocumentChunks_fts"(rowid, "HeadingPath", "Text") VALUES (new."Id", new."HeadingPath", new."Text");
                END;
                """);

            migrationBuilder.Sql("""INSERT INTO "DocumentChunks_fts"("DocumentChunks_fts") VALUES ('rebuild');""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TRIGGER IF EXISTS "DocumentChunks_fts_au";""");
            migrationBuilder.Sql("""DROP TRIGGER IF EXISTS "DocumentChunks_fts_ad";""");
            migrationBuilder.Sql("""DROP TRIGGER IF EXISTS "DocumentChunks_fts_ai";""");
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "DocumentChunks_fts";""");
        }
    }
}
