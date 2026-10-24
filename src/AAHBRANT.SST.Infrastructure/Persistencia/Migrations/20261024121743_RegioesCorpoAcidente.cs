using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class RegioesCorpoAcidente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RegioesCorpo",
                table: "Acidentes",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RegioesCorpo",
                table: "Acidentes");
        }
    }
}
