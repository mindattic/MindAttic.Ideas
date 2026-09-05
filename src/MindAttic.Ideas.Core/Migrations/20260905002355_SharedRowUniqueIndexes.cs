using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MindAttic.Ideas.Core.Migrations
{
    /// <summary>
    /// Restores the uniqueness A36 silently dropped. Making SiteId part of the identity produced a unique
    /// index over a nullable column, which SQL Server filters to IS NOT NULL — leaving every SHARED row
    /// (every row predating A36, and every row the library seeder installs) unconstrained. These two
    /// complementary IS NULL indexes cover exactly the rows the other one cannot, so the pre-A36 guarantee
    /// is back and the install path’s concurrency guard — a caught DbUpdateException from this very
    /// index — works again for a shared install.
    /// </summary>
    public partial class SharedRowUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_InstalledPackages_Category_Key_Version_Shared",
                table: "InstalledPackages",
                columns: new[] { "Category", "Key", "Version" },
                unique: true,
                filter: "[SiteId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContentDefinitions_Kind_Key_Version_Origin_Shared",
                table: "ContentDefinitions",
                columns: new[] { "Kind", "Key", "Version", "Origin" },
                unique: true,
                filter: "[SiteId] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InstalledPackages_Category_Key_Version_Shared",
                table: "InstalledPackages");

            migrationBuilder.DropIndex(
                name: "IX_ContentDefinitions_Kind_Key_Version_Origin_Shared",
                table: "ContentDefinitions");
        }
    }
}
