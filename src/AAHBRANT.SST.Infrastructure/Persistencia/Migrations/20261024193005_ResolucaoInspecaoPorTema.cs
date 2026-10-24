using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class ResolucaoInspecaoPorTema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ResolucaoTemaInspecaoId",
                table: "AcoesPlano",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcoesPlano_ResolucaoTemaInspecaoId",
                table: "AcoesPlano",
                column: "ResolucaoTemaInspecaoId");

            migrationBuilder.AddForeignKey(
                name: "FK_AcoesPlano_TemasInspecao_ResolucaoTemaInspecaoId",
                table: "AcoesPlano",
                column: "ResolucaoTemaInspecaoId",
                principalTable: "TemasInspecao",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcoesPlano_TemasInspecao_ResolucaoTemaInspecaoId",
                table: "AcoesPlano");

            migrationBuilder.DropIndex(
                name: "IX_AcoesPlano_ResolucaoTemaInspecaoId",
                table: "AcoesPlano");

            migrationBuilder.DropColumn(
                name: "ResolucaoTemaInspecaoId",
                table: "AcoesPlano");
        }
    }
}
