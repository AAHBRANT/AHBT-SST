using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCorCapaceteEOculosCamera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AvisoCorCapacete",
                table: "OcorrenciasEpiCamera",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CapaceteNaoAvaliavel",
                table: "OcorrenciasEpiCamera",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ColeteNaoAvaliavel",
                table: "OcorrenciasEpiCamera",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CorCapaceteEsperada",
                table: "OcorrenciasEpiCamera",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CorCapaceteVista",
                table: "OcorrenciasEpiCamera",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OculosNaoAvaliavel",
                table: "OcorrenciasEpiCamera",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SemOculos",
                table: "OcorrenciasEpiCamera",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CorCapacete",
                table: "Funcoes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimaIdentificacaoSoCorEmUtc",
                table: "Cameras",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvisoCorCapacete",
                table: "OcorrenciasEpiCamera");

            migrationBuilder.DropColumn(
                name: "CapaceteNaoAvaliavel",
                table: "OcorrenciasEpiCamera");

            migrationBuilder.DropColumn(
                name: "ColeteNaoAvaliavel",
                table: "OcorrenciasEpiCamera");

            migrationBuilder.DropColumn(
                name: "CorCapaceteEsperada",
                table: "OcorrenciasEpiCamera");

            migrationBuilder.DropColumn(
                name: "CorCapaceteVista",
                table: "OcorrenciasEpiCamera");

            migrationBuilder.DropColumn(
                name: "OculosNaoAvaliavel",
                table: "OcorrenciasEpiCamera");

            migrationBuilder.DropColumn(
                name: "SemOculos",
                table: "OcorrenciasEpiCamera");

            migrationBuilder.DropColumn(
                name: "CorCapacete",
                table: "Funcoes");

            migrationBuilder.DropColumn(
                name: "UltimaIdentificacaoSoCorEmUtc",
                table: "Cameras");
        }
    }
}
