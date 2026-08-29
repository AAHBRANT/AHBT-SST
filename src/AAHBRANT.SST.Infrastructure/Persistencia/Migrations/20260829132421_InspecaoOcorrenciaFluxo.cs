using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class InspecaoOcorrenciaFluxo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InspecaoItemRespostaId",
                table: "NaoConformidades",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoDevolucao",
                table: "NaoConformidades",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NaoConformidades_InspecaoItemRespostaId",
                table: "NaoConformidades",
                column: "InspecaoItemRespostaId",
                unique: true,
                filter: "[InspecaoItemRespostaId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_NaoConformidades_InspecaoItemRespostas_InspecaoItemRespostaId",
                table: "NaoConformidades",
                column: "InspecaoItemRespostaId",
                principalTable: "InspecaoItemRespostas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NaoConformidades_InspecaoItemRespostas_InspecaoItemRespostaId",
                table: "NaoConformidades");

            migrationBuilder.DropIndex(
                name: "IX_NaoConformidades_InspecaoItemRespostaId",
                table: "NaoConformidades");

            migrationBuilder.DropColumn(
                name: "InspecaoItemRespostaId",
                table: "NaoConformidades");

            migrationBuilder.DropColumn(
                name: "MotivoDevolucao",
                table: "NaoConformidades");
        }
    }
}
