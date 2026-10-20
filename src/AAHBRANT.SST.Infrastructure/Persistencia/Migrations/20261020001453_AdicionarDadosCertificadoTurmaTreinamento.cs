using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarDadosCertificadoTurmaTreinamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConteudoProgramatico",
                table: "Treinamentos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConteudoProgramatico",
                table: "SessoesTreinamento",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstrutorRegistroProfissional",
                table: "SessoesTreinamento",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Local",
                table: "SessoesTreinamento",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValidadeEmMeses",
                table: "SessoesTreinamento",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConteudoProgramatico",
                table: "Treinamentos");

            migrationBuilder.DropColumn(
                name: "ConteudoProgramatico",
                table: "SessoesTreinamento");

            migrationBuilder.DropColumn(
                name: "InstrutorRegistroProfissional",
                table: "SessoesTreinamento");

            migrationBuilder.DropColumn(
                name: "Local",
                table: "SessoesTreinamento");

            migrationBuilder.DropColumn(
                name: "ValidadeEmMeses",
                table: "SessoesTreinamento");
        }
    }
}
