using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarConclusaoManualAcaoPlano : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConcluidoPorUsuarioId",
                table: "AcoesPlano",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescricaoConclusao",
                table: "AcoesPlano",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcoesPlano_ConcluidoPorUsuarioId",
                table: "AcoesPlano",
                column: "ConcluidoPorUsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_AcoesPlano_Usuarios_ConcluidoPorUsuarioId",
                table: "AcoesPlano",
                column: "ConcluidoPorUsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcoesPlano_Usuarios_ConcluidoPorUsuarioId",
                table: "AcoesPlano");

            migrationBuilder.DropIndex(
                name: "IX_AcoesPlano_ConcluidoPorUsuarioId",
                table: "AcoesPlano");

            migrationBuilder.DropColumn(
                name: "ConcluidoPorUsuarioId",
                table: "AcoesPlano");

            migrationBuilder.DropColumn(
                name: "DescricaoConclusao",
                table: "AcoesPlano");
        }
    }
}
