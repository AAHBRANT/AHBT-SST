using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class IdentificacaoFacialOcorrenciaEpi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ConfiancaIdentificacao",
                table: "OcorrenciasEpiCamera",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IdentificadoPorRosto",
                table: "OcorrenciasEpiCamera",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfiancaIdentificacao",
                table: "OcorrenciasEpiCamera");

            migrationBuilder.DropColumn(
                name: "IdentificadoPorRosto",
                table: "OcorrenciasEpiCamera");
        }
    }
}
