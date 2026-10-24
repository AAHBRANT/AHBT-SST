using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarReuniaoAnaliseRealizada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Ata",
                table: "ReunioesAnaliseOcorrencia",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParticipantesUsuarioIds",
                table: "ReunioesAnaliseOcorrencia",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Presentes",
                table: "ReunioesAnaliseOcorrencia",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RealizadaEm",
                table: "ReunioesAnaliseOcorrencia",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Ata",
                table: "ReunioesAnaliseOcorrencia");

            migrationBuilder.DropColumn(
                name: "ParticipantesUsuarioIds",
                table: "ReunioesAnaliseOcorrencia");

            migrationBuilder.DropColumn(
                name: "Presentes",
                table: "ReunioesAnaliseOcorrencia");

            migrationBuilder.DropColumn(
                name: "RealizadaEm",
                table: "ReunioesAnaliseOcorrencia");
        }
    }
}
