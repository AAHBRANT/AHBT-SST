using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarReconhecimentoFacial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DdsSemanais_ObraId",
                table: "DdsSemanais");

            migrationBuilder.AddColumn<string>(
                name: "AzureFacePersonId",
                table: "Trabalhadores",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AzureFacePersonGroupId",
                table: "Obras",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AzureFacePersonId",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "AzureFacePersonGroupId",
                table: "Obras");

            migrationBuilder.CreateIndex(
                name: "IX_DdsSemanais_ObraId",
                table: "DdsSemanais",
                column: "ObraId");
        }
    }
}
