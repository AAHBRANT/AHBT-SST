using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarRegrasAlertaESombraCamera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Confirmada",
                table: "OcorrenciasEpiCamera",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmadaEmUtc",
                table: "OcorrenciasEpiCamera",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PosicaoX",
                table: "OcorrenciasEpiCamera",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PosicaoY",
                table: "OcorrenciasEpiCamera",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SegundosObservados",
                table: "OcorrenciasEpiCamera",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Sombra",
                table: "OcorrenciasEpiCamera",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ModoSombra",
                table: "Cameras",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "NaoAvaliaveisNoDia",
                table: "Cameras",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NaoRepetirSegundos",
                table: "Cameras",
                type: "int",
                nullable: false,
                defaultValue: 300);

            migrationBuilder.AddColumn<int>(
                name: "PersistenciaCapaceteSegundos",
                table: "Cameras",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "PersistenciaColeteSegundos",
                table: "Cameras",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "PersistenciaOculosSegundos",
                table: "Cameras",
                type: "int",
                nullable: false,
                defaultValue: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Confirmada",
                table: "OcorrenciasEpiCamera");

            migrationBuilder.DropColumn(
                name: "ConfirmadaEmUtc",
                table: "OcorrenciasEpiCamera");

            migrationBuilder.DropColumn(
                name: "PosicaoX",
                table: "OcorrenciasEpiCamera");

            migrationBuilder.DropColumn(
                name: "PosicaoY",
                table: "OcorrenciasEpiCamera");

            migrationBuilder.DropColumn(
                name: "SegundosObservados",
                table: "OcorrenciasEpiCamera");

            migrationBuilder.DropColumn(
                name: "Sombra",
                table: "OcorrenciasEpiCamera");

            migrationBuilder.DropColumn(
                name: "ModoSombra",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "NaoAvaliaveisNoDia",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "NaoRepetirSegundos",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "PersistenciaCapaceteSegundos",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "PersistenciaColeteSegundos",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "PersistenciaOculosSegundos",
                table: "Cameras");
        }
    }
}
