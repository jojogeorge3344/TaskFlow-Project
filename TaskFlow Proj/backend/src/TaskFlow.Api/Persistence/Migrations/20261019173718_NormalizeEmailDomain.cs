using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Api.Persistence.Migrations
{
    /// <summary>
    /// Moves every existing user onto the fixed <c>@taskflow.com</c> domain. The local part is kept, except that characters
    /// the new rules reject are dropped (<c>o'brien</c> becomes <c>obrien</c>), stray or doubled dots are tidied, it is cut to
    /// 64 characters, and a part left empty becomes <c>user&lt;id&gt;</c>. Users are taken in creation order, so when two addresses collapse to the same local part the later one gets a number
    /// (<c>jo</c>, <c>jo2</c>). Deleted users are converted too because their address stays reserved. Running it again
    /// changes nothing.
    /// </summary>
    public partial class NormalizeEmailDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TEMP TABLE ""EmailDomainMap"" AS
WITH RECURSIVE raw AS (
    SELECT ""Id"",
           lower(trim(CASE WHEN instr(""Email"", '@') > 0 THEN substr(""Email"", 1, instr(""Email"", '@') - 1) ELSE ""Email"" END)) AS ""Raw""
    FROM ""Users""
),
-- Keep only the characters a local part may hold: letters, digits, dot, underscore and hyphen.
walk(""Id"", ""Pos"", ""Raw"", ""Clean"") AS (
    SELECT ""Id"", 1, ""Raw"", '' FROM raw
    UNION ALL
    SELECT ""Id"", ""Pos"" + 1, ""Raw"",
           ""Clean"" || CASE WHEN instr('abcdefghijklmnopqrstuvwxyz0123456789._-', substr(""Raw"", ""Pos"", 1)) > 0
                           THEN substr(""Raw"", ""Pos"", 1) ELSE '' END
    FROM walk WHERE ""Pos"" <= length(""Raw"")
),
cleaned AS (
    SELECT ""Id"", ""Clean"" FROM walk WHERE ""Pos"" > length(""Raw"")
),
locals AS (
    SELECT ""Id"",
           CASE WHEN ""Tidy"" = '' THEN 'user' || ""Id"" ELSE ""Tidy"" END AS ""Local""
    FROM (
        SELECT ""Id"",
               trim(substr(trim(replace(replace(replace(replace(replace(replace(""Clean"", '..', '.'), '..', '.'), '..', '.'), '..', '.'), '..', '.'), '..', '.'), '.'), 1, 64), '.') AS ""Tidy""
        FROM cleaned
    )
),
ranked AS (
    SELECT ""Id"", ""Local"",
           ROW_NUMBER() OVER (PARTITION BY ""Local"" ORDER BY ""Id"") AS ""Rn""
    FROM locals
),
candidates AS (
    SELECT ""Id"", ""Rn"",
           CASE WHEN ""Rn"" = 1 THEN ""Local"" ELSE ""Local"" || ""Rn"" END AS ""Candidate""
    FROM ranked
),
final AS (
    -- A numbered candidate can still equal another user's own local part (jo2); the plain address wins, the
    -- numbered one falls back to a name that carries its id.
    SELECT ""Id"",
           CASE WHEN ROW_NUMBER() OVER (PARTITION BY ""Candidate"" ORDER BY (""Rn"" = 1) DESC, ""Id"") = 1
                THEN ""Candidate""
                ELSE ""Candidate"" || '.' || ""Id"" END AS ""Local""
    FROM candidates
)
SELECT ""Id"", ""Local"" || '@taskflow.com' AS ""NewEmail"" FROM final;");

            // Two steps so that an address handed from one user to another never collides with the unique index mid-way.
            migrationBuilder.Sql(@"UPDATE ""Users"" SET ""Email"" = 'migrating:' || ""Id"";");
            migrationBuilder.Sql(@"UPDATE ""Users"" SET ""Email"" = (SELECT ""NewEmail"" FROM ""EmailDomainMap"" WHERE ""EmailDomainMap"".""Id"" = ""Users"".""Id"");");
            migrationBuilder.Sql(@"DROP TABLE ""EmailDomainMap"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The original domains are not stored, so they cannot be restored.
        }
    }
}
