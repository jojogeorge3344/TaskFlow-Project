using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Infrastructure.Migrations
{
    /// <summary>
    /// Moves every existing user onto the fixed <c>@taskflow.com</c> domain. The local part is kept; users are taken in
    /// creation order, so when two addresses collapse to the same local part the later one gets a number
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
WITH locals AS (
    SELECT ""Id"",
           lower(trim(CASE WHEN instr(""Email"", '@') > 0 THEN substr(""Email"", 1, instr(""Email"", '@') - 1) ELSE ""Email"" END)) AS ""Local""
    FROM ""Users""
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
