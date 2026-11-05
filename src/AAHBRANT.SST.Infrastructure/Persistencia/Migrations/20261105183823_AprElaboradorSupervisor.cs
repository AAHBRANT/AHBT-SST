using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AprElaboradorSupervisor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ElaboradorTrabalhadorId",
                table: "Aprs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupervisorTrabalhadorId",
                table: "Aprs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Aprs_ElaboradorTrabalhadorId",
                table: "Aprs",
                column: "ElaboradorTrabalhadorId");

            migrationBuilder.CreateIndex(
                name: "IX_Aprs_SupervisorTrabalhadorId",
                table: "Aprs",
                column: "SupervisorTrabalhadorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Aprs_Trabalhadores_ElaboradorTrabalhadorId",
                table: "Aprs",
                column: "ElaboradorTrabalhadorId",
                principalTable: "Trabalhadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Aprs_Trabalhadores_SupervisorTrabalhadorId",
                table: "Aprs",
                column: "SupervisorTrabalhadorId",
                principalTable: "Trabalhadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Aprs_Trabalhadores_ElaboradorTrabalhadorId",
                table: "Aprs");

            migrationBuilder.DropForeignKey(
                name: "FK_Aprs_Trabalhadores_SupervisorTrabalhadorId",
                table: "Aprs");

            migrationBuilder.DropIndex(
                name: "IX_Aprs_ElaboradorTrabalhadorId",
                table: "Aprs");

            migrationBuilder.DropIndex(
                name: "IX_Aprs_SupervisorTrabalhadorId",
                table: "Aprs");

            migrationBuilder.DropColumn(
                name: "ElaboradorTrabalhadorId",
                table: "Aprs");

            migrationBuilder.DropColumn(
                name: "SupervisorTrabalhadorId",
                table: "Aprs");
        }
    }
}
