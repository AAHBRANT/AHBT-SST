using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarPapelAssinatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentoSignatarios_DocumentoAssinaturaId_TrabalhadorId_MetodoAutenticacao",
                table: "DocumentoSignatarios");

            migrationBuilder.AddColumn<int>(
                name: "Papel",
                table: "DocumentoSignatarios",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoSignatarios_DocumentoAssinaturaId_TrabalhadorId_MetodoAutenticacao_Papel",
                table: "DocumentoSignatarios",
                columns: new[] { "DocumentoAssinaturaId", "TrabalhadorId", "MetodoAutenticacao", "Papel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentoSignatarios_DocumentoAssinaturaId_TrabalhadorId_MetodoAutenticacao_Papel",
                table: "DocumentoSignatarios");

            migrationBuilder.DropColumn(
                name: "Papel",
                table: "DocumentoSignatarios");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoSignatarios_DocumentoAssinaturaId_TrabalhadorId_MetodoAutenticacao",
                table: "DocumentoSignatarios",
                columns: new[] { "DocumentoAssinaturaId", "TrabalhadorId", "MetodoAutenticacao" },
                unique: true);
        }
    }
}
