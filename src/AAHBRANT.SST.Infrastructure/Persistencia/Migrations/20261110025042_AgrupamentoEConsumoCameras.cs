using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AgrupamentoEConsumoCameras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UltimaVezEmUtc",
                table: "OcorrenciasEpiCamera",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VezesVista",
                table: "OcorrenciasEpiCamera",
                type: "int",
                nullable: false,
                defaultValue: 1); // ocorrências já gravadas foram vistas uma vez

            migrationBuilder.AddColumn<int>(
                name: "AnalisesNoDia",
                table: "Cameras",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DiaDoConsumo",
                table: "Cameras",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TokensEntradaNoDia",
                table: "Cameras",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TokensSaidaNoDia",
                table: "Cameras",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TotalTokensEntrada",
                table: "Cameras",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TotalTokensSaida",
                table: "Cameras",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UltimaVezEmUtc",
                table: "OcorrenciasEpiCamera");

            migrationBuilder.DropColumn(
                name: "VezesVista",
                table: "OcorrenciasEpiCamera");

            migrationBuilder.DropColumn(
                name: "AnalisesNoDia",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "DiaDoConsumo",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "TokensEntradaNoDia",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "TokensSaidaNoDia",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "TotalTokensEntrada",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "TotalTokensSaida",
                table: "Cameras");
        }
    }
}
