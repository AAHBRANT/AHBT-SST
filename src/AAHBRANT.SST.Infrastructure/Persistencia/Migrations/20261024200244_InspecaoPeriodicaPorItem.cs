using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class InspecaoPeriodicaPorItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PeriodicidadeInspecaoDias",
                table: "Veiculos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResponsavelInspecaoUsuarioId",
                table: "Veiculos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PeriodicidadeDiasPadrao",
                table: "TemasInspecao",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PeriodicidadeInspecaoDias",
                table: "ItensInspecionaveis",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResponsavelInspecaoUsuarioId",
                table: "ItensInspecionaveis",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Veiculos_ResponsavelInspecaoUsuarioId",
                table: "Veiculos",
                column: "ResponsavelInspecaoUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensInspecionaveis_ResponsavelInspecaoUsuarioId",
                table: "ItensInspecionaveis",
                column: "ResponsavelInspecaoUsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_ItensInspecionaveis_Usuarios_ResponsavelInspecaoUsuarioId",
                table: "ItensInspecionaveis",
                column: "ResponsavelInspecaoUsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Veiculos_Usuarios_ResponsavelInspecaoUsuarioId",
                table: "Veiculos",
                column: "ResponsavelInspecaoUsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ItensInspecionaveis_Usuarios_ResponsavelInspecaoUsuarioId",
                table: "ItensInspecionaveis");

            migrationBuilder.DropForeignKey(
                name: "FK_Veiculos_Usuarios_ResponsavelInspecaoUsuarioId",
                table: "Veiculos");

            migrationBuilder.DropIndex(
                name: "IX_Veiculos_ResponsavelInspecaoUsuarioId",
                table: "Veiculos");

            migrationBuilder.DropIndex(
                name: "IX_ItensInspecionaveis_ResponsavelInspecaoUsuarioId",
                table: "ItensInspecionaveis");

            migrationBuilder.DropColumn(
                name: "PeriodicidadeInspecaoDias",
                table: "Veiculos");

            migrationBuilder.DropColumn(
                name: "ResponsavelInspecaoUsuarioId",
                table: "Veiculos");

            migrationBuilder.DropColumn(
                name: "PeriodicidadeDiasPadrao",
                table: "TemasInspecao");

            migrationBuilder.DropColumn(
                name: "PeriodicidadeInspecaoDias",
                table: "ItensInspecionaveis");

            migrationBuilder.DropColumn(
                name: "ResponsavelInspecaoUsuarioId",
                table: "ItensInspecionaveis");
        }
    }
}
