using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarFotoEvidenciaAssinatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FotoEvidenciaContentType",
                table: "DocumentoSignatarios",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "FotoEvidenciaConteudo",
                table: "DocumentoSignatarios",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FotoEvidenciaHash",
                table: "DocumentoSignatarios",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FotoEvidenciaContentType",
                table: "DocumentoSignatarios");

            migrationBuilder.DropColumn(
                name: "FotoEvidenciaConteudo",
                table: "DocumentoSignatarios");

            migrationBuilder.DropColumn(
                name: "FotoEvidenciaHash",
                table: "DocumentoSignatarios");
        }
    }
}
