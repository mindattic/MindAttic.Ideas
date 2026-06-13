using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MindAttic.Ideas.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddPageMetaTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PageMetaTags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PageId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageMetaTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PageMetaTags_Pages_PageId",
                        column: x => x.PageId,
                        principalTable: "Pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PageMetaTags_PageId_Name",
                table: "PageMetaTags",
                columns: new[] { "PageId", "Name" },
                unique: true);

            // Migrate existing SeoMetaJson data into PageMetaTags rows before dropping the column.
            migrationBuilder.Sql("""
                INSERT INTO PageMetaTags (PageId, Name, Content)
                SELECT Id, 'seo.title', JSON_VALUE(SeoMetaJson, '$.title')
                FROM Pages
                WHERE SeoMetaJson IS NOT NULL AND JSON_VALUE(SeoMetaJson, '$.title') IS NOT NULL;

                INSERT INTO PageMetaTags (PageId, Name, Content)
                SELECT Id, 'seo.description', JSON_VALUE(SeoMetaJson, '$.description')
                FROM Pages
                WHERE SeoMetaJson IS NOT NULL AND JSON_VALUE(SeoMetaJson, '$.description') IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "SeoMetaJson",
                table: "Pages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PageMetaTags");

            migrationBuilder.AddColumn<string>(
                name: "SeoMetaJson",
                table: "Pages",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
