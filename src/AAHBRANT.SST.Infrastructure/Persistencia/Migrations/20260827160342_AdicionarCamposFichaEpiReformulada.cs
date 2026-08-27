using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCamposFichaEpiReformulada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Turno",
                table: "Trabalhadores",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cnpj",
                table: "Obras",
                type: "nvarchar(18)",
                maxLength: 18,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoContentType",
                table: "Obras",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "LogoConteudo",
                table: "Obras",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataTreinamentoNr6",
                table: "EntregasEpi",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MotivoTipo",
                table: "EntregasEpi",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroListaPresencaNr6",
                table: "EntregasEpi",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Turno",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "Cnpj",
                table: "Obras");

            migrationBuilder.DropColumn(
                name: "LogoContentType",
                table: "Obras");

            migrationBuilder.DropColumn(
                name: "LogoConteudo",
                table: "Obras");

            migrationBuilder.DropColumn(
                name: "DataTreinamentoNr6",
                table: "EntregasEpi");

            migrationBuilder.DropColumn(
                name: "MotivoTipo",
                table: "EntregasEpi");

            migrationBuilder.DropColumn(
                name: "NumeroListaPresencaNr6",
                table: "EntregasEpi");
        }
    }
}
