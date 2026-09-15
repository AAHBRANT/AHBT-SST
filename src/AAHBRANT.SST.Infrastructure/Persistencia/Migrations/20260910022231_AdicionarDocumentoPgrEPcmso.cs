using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarDocumentoPgrEPcmso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Arquivo",
                table: "PcmsoDetalhes");

            migrationBuilder.AddColumn<string>(
                name: "DocumentoContentType",
                table: "Pgrs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "DocumentoConteudo",
                table: "Pgrs",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentoContentType",
                table: "PcmsoDetalhes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "DocumentoConteudo",
                table: "PcmsoDetalhes",
                type: "varbinary(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentoContentType",
                table: "Pgrs");

            migrationBuilder.DropColumn(
                name: "DocumentoConteudo",
                table: "Pgrs");

            migrationBuilder.DropColumn(
                name: "DocumentoContentType",
                table: "PcmsoDetalhes");

            migrationBuilder.DropColumn(
                name: "DocumentoConteudo",
                table: "PcmsoDetalhes");

            migrationBuilder.AddColumn<string>(
                name: "Arquivo",
                table: "PcmsoDetalhes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
