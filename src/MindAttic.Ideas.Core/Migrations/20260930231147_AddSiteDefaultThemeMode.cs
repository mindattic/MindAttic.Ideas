using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MindAttic.Ideas.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteDefaultThemeMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultThemeMode",
                table: "Sites",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "light");

            // The standalone "Light" and "Dark" themes were merged into one theme, "ideas", with light
            // and dark modes. Any site/page still pointing at the old keys would otherwise silently
            // fall back to the unrelated built-in "bootstrap" theme once those keys stop resolving.
            migrationBuilder.Sql("UPDATE Sites SET DefaultThemeKey = 'ideas', DefaultThemeMode = 'light' WHERE DefaultThemeKey = 'light';");
            migrationBuilder.Sql("UPDATE Sites SET DefaultThemeKey = 'ideas', DefaultThemeMode = 'dark' WHERE DefaultThemeKey = 'dark';");
            migrationBuilder.Sql("UPDATE Pages SET ThemeKey = 'ideas' WHERE ThemeKey IN ('light', 'dark');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultThemeMode",
                table: "Sites");
        }
    }
}
