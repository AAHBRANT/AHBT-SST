using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarFluxoAprovacaoSuporteIa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AprovadoEmUtc",
                table: "SuporteIaSolicitacoes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComentarioValidacao",
                table: "SuporteIaSolicitacoes",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConcluidoEmUtc",
                table: "SuporteIaSolicitacoes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotaFechamento",
                table: "SuporteIaSolicitacoes",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsavelNome",
                table: "SuporteIaSolicitacoes",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResponsavelUsuarioId",
                table: "SuporteIaSolicitacoes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ValidacaoConfirmada",
                table: "SuporteIaSolicitacoes",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidadoEmUtc",
                table: "SuporteIaSolicitacoes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SuporteIaSolicitacoes_ResponsavelUsuarioId",
                table: "SuporteIaSolicitacoes",
                column: "ResponsavelUsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_SuporteIaSolicitacoes_Usuarios_ResponsavelUsuarioId",
                table: "SuporteIaSolicitacoes",
                column: "ResponsavelUsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SuporteIaSolicitacoes_Usuarios_ResponsavelUsuarioId",
                table: "SuporteIaSolicitacoes");

            migrationBuilder.DropIndex(
                name: "IX_SuporteIaSolicitacoes_ResponsavelUsuarioId",
                table: "SuporteIaSolicitacoes");

            migrationBuilder.DropColumn(
                name: "AprovadoEmUtc",
                table: "SuporteIaSolicitacoes");

            migrationBuilder.DropColumn(
                name: "ComentarioValidacao",
                table: "SuporteIaSolicitacoes");

            migrationBuilder.DropColumn(
                name: "ConcluidoEmUtc",
                table: "SuporteIaSolicitacoes");

            migrationBuilder.DropColumn(
                name: "NotaFechamento",
                table: "SuporteIaSolicitacoes");

            migrationBuilder.DropColumn(
                name: "ResponsavelNome",
                table: "SuporteIaSolicitacoes");

            migrationBuilder.DropColumn(
                name: "ResponsavelUsuarioId",
                table: "SuporteIaSolicitacoes");

            migrationBuilder.DropColumn(
                name: "ValidacaoConfirmada",
                table: "SuporteIaSolicitacoes");

            migrationBuilder.DropColumn(
                name: "ValidadoEmUtc",
                table: "SuporteIaSolicitacoes");
        }
    }
}
