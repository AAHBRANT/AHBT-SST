using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarResolucaoVinculadaAcaoPlano : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ResolucaoCatalogoEpiId",
                table: "AcoesPlano",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResolucaoCursoTreinamentoId",
                table: "AcoesPlano",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResolucaoTipoInspecao",
                table: "AcoesPlano",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TipoResolucao",
                table: "AcoesPlano",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcoesPlano_ResolucaoCatalogoEpiId",
                table: "AcoesPlano",
                column: "ResolucaoCatalogoEpiId");

            migrationBuilder.CreateIndex(
                name: "IX_AcoesPlano_ResolucaoCursoTreinamentoId",
                table: "AcoesPlano",
                column: "ResolucaoCursoTreinamentoId");

            migrationBuilder.AddForeignKey(
                name: "FK_AcoesPlano_CatalogoEpis_ResolucaoCatalogoEpiId",
                table: "AcoesPlano",
                column: "ResolucaoCatalogoEpiId",
                principalTable: "CatalogoEpis",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AcoesPlano_CursosTreinamento_ResolucaoCursoTreinamentoId",
                table: "AcoesPlano",
                column: "ResolucaoCursoTreinamentoId",
                principalTable: "CursosTreinamento",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcoesPlano_CatalogoEpis_ResolucaoCatalogoEpiId",
                table: "AcoesPlano");

            migrationBuilder.DropForeignKey(
                name: "FK_AcoesPlano_CursosTreinamento_ResolucaoCursoTreinamentoId",
                table: "AcoesPlano");

            migrationBuilder.DropIndex(
                name: "IX_AcoesPlano_ResolucaoCatalogoEpiId",
                table: "AcoesPlano");

            migrationBuilder.DropIndex(
                name: "IX_AcoesPlano_ResolucaoCursoTreinamentoId",
                table: "AcoesPlano");

            migrationBuilder.DropColumn(
                name: "ResolucaoCatalogoEpiId",
                table: "AcoesPlano");

            migrationBuilder.DropColumn(
                name: "ResolucaoCursoTreinamentoId",
                table: "AcoesPlano");

            migrationBuilder.DropColumn(
                name: "ResolucaoTipoInspecao",
                table: "AcoesPlano");

            migrationBuilder.DropColumn(
                name: "TipoResolucao",
                table: "AcoesPlano");
        }
    }
}
