using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class OperadorEConsentimentoBiometria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ConsentimentoBiometriaRevogadoEm",
                table: "Trabalhadores",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsentimentoBiometriaVersao",
                table: "Trabalhadores",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OperadorUsuarioId",
                table: "DocumentoSignatarios",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsentimentoBiometriaRevogadoEm",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "ConsentimentoBiometriaVersao",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "OperadorUsuarioId",
                table: "DocumentoSignatarios");
        }
    }
}
