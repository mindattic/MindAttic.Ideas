using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MindAttic.Ideas.Core.Migrations
{
    /// <inheritdoc />
    public partial class RemoveControlKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data heal for the Control-kind REMOVAL (amendment MAI-A19). The `Control` ContentKind ordinal
            // was deleted from the frozen enum (lone pre-1.0 exception), so rows still carrying the string
            // 'Control' would no longer map and EF would throw on the first read (DiscoveryService). This
            // migration runs in MigrateAsync BEFORE discovery, so any existing database self-heals. Atomic
            // UI is now a Widget — author include-tags are rewritten so already-published pages keep
            // resolving (the seeded Textbox demo etc.). Forward-only; Down is a no-op.
            migrationBuilder.Sql("DELETE FROM [ContentDefinitions] WHERE [Kind] = 'Control';");
            migrationBuilder.Sql("DELETE FROM [InstalledPackages] WHERE [Category] = 'Control';");
            migrationBuilder.Sql("UPDATE [Pages] SET [BodyHtml] = REPLACE([BodyHtml], 'MindAttic.Ideas.Control.', 'MindAttic.Ideas.Widget.') WHERE [BodyHtml] LIKE '%MindAttic.Ideas.Control.%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reversible: the Control kind no longer exists. Forward-only (per MAI-A19).
        }
    }
}
