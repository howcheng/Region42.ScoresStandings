using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Region42.ScoresStandings.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamShortName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShortName",
                table: "Teams",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            // Populate ShortName from Name for existing teams
            // Format: <division><number> <fun name> (<coach>) → <number> <fun name>
            // Format: <division><number> (<coach>) → <number> <coach>
            // Max 20 characters with ellipsis if truncated
            migrationBuilder.Sql(@"
                UPDATE ""Teams"" 
                SET ""ShortName"" = 
                    CASE 
                        -- If name contains '(' and ')', we can extract coach name
                        WHEN POSITION('(' IN ""Name"") > 0 AND POSITION(')' IN ""Name"") > POSITION('(' IN ""Name"") THEN
                            CASE
                                -- Try to extract team number and check if fun name exists
                                WHEN ""Name"" ~ '^[0-9A-Z]{2,6}[0-9]+\s+[A-Za-z]' THEN
                                    -- Has fun name: Extract number + fun name (e.g., '01 Jets')
                                    CASE
                                        WHEN LENGTH(TRIM(REGEXP_REPLACE(SUBSTRING(""Name"" FROM '^[0-9A-Z]{2,6}([0-9].+)\\s*\\('), '\\s*\\($', ''))) > 20 THEN
                                            SUBSTRING(TRIM(REGEXP_REPLACE(SUBSTRING(""Name"" FROM '^[0-9A-Z]{2,6}([0-9].+)\\s*\\('), '\\s*\\($', '')), 1, 19) || '…'
                                        ELSE
                                            TRIM(REGEXP_REPLACE(SUBSTRING(""Name"" FROM '^[0-9A-Z]{2,6}([0-9].+)\\s*\\('), '\\s*\\($', ''))
                                    END
                                WHEN ""Name"" ~ '^[0-9A-Z]{2,6}[0-9]+\s*\\(' THEN
                                    -- No fun name: Extract number + coach (e.g., '01 Smith')
                                    CASE
                                        WHEN LENGTH(
                                            TRIM(REGEXP_REPLACE(SUBSTRING(""Name"" FROM '^[0-9A-Z]{2,6}([0-9]+)'), '\\s*$', '')) || ' ' ||
                                            TRIM(SUBSTRING(""Name"" FROM '\\(([^)]+)\\)'))
                                        ) > 20 THEN
                                            SUBSTRING(
                                                TRIM(REGEXP_REPLACE(SUBSTRING(""Name"" FROM '^[0-9A-Z]{2,6}([0-9]+)'), '\\s*$', '')) || ' ' ||
                                                TRIM(SUBSTRING(""Name"" FROM '\\(([^)]+)\\)')),
                                                1, 19
                                            ) || '…'
                                        ELSE
                                            TRIM(REGEXP_REPLACE(SUBSTRING(""Name"" FROM '^[0-9A-Z]{2,6}([0-9]+)'), '\\s*$', '')) || ' ' ||
                                            TRIM(SUBSTRING(""Name"" FROM '\\(([^)]+)\\)'))
                                    END
                                ELSE
                                    -- Fallback: use name without coach, truncate if needed
                                    CASE
                                        WHEN LENGTH(TRIM(SUBSTRING(""Name"" FROM 1 FOR POSITION('(' IN ""Name"") - 1))) > 20 THEN
                                            SUBSTRING(TRIM(SUBSTRING(""Name"" FROM 1 FOR POSITION('(' IN ""Name"") - 1)), 1, 19) || '…'
                                        ELSE
                                            TRIM(SUBSTRING(""Name"" FROM 1 FOR POSITION('(' IN ""Name"") - 1))
                                    END
                            END
                        ELSE
                            -- No coach name, just truncate if needed
                            CASE
                                WHEN LENGTH(""Name"") > 20 THEN SUBSTRING(""Name"", 1, 19) || '…'
                                ELSE ""Name""
                            END
                    END
                WHERE ""ShortName"" = '';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShortName",
                table: "Teams");
        }
    }
}
