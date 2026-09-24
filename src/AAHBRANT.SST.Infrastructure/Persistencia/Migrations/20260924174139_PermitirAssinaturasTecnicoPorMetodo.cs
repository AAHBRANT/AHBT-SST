using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class PermitirAssinaturasTecnicoPorMetodo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentoSignatarios_DocumentoAssinaturaId_TrabalhadorId",
                table: "DocumentoSignatarios");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoSignatarios_DocumentoAssinaturaId_TrabalhadorId_MetodoAutenticacao",
                table: "DocumentoSignatarios",
                columns: new[] { "DocumentoAssinaturaId", "TrabalhadorId", "MetodoAutenticacao" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentoSignatarios_DocumentoAssinaturaId_TrabalhadorId_MetodoAutenticacao",
                table: "DocumentoSignatarios");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoSignatarios_DocumentoAssinaturaId_TrabalhadorId",
                table: "DocumentoSignatarios",
                columns: new[] { "DocumentoAssinaturaId", "TrabalhadorId" },
                unique: true);
        }
    }
}
