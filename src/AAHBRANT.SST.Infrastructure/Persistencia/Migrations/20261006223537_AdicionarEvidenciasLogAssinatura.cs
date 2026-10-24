using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarEvidenciasLogAssinatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Inspecoes_AlojamentoId",
                table: "Inspecoes");

            migrationBuilder.DropIndex(
                name: "IX_Inspecoes_VeiculoId",
                table: "Inspecoes");

            migrationBuilder.AddColumn<string>(
                name: "ImagemCadastroCriptografada",
                table: "TemplatesBiometricoFutronic",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DispositivoAgenteId",
                table: "DocumentoSignatarios",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "DocumentoSignatarios",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LocalizacaoStatus",
                table: "DocumentoSignatarios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "DocumentoSignatarios",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PrecisaoMetros",
                table: "DocumentoSignatarios",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "DocumentoSignatarios",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidacaoGrupoId",
                table: "DocumentoSignatarios",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidacaoModelo",
                table: "DocumentoSignatarios",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidacaoRequisicaoId",
                table: "DocumentoSignatarios",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inspecoes_AlojamentoId",
                table: "Inspecoes",
                column: "AlojamentoId",
                unique: true,
                filter: "[AlojamentoId] IS NOT NULL AND [Status] = 1 AND [Ativo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Inspecoes_VeiculoId",
                table: "Inspecoes",
                column: "VeiculoId",
                unique: true,
                filter: "[VeiculoId] IS NOT NULL AND [Status] = 1 AND [Ativo] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Inspecoes_AlojamentoId",
                table: "Inspecoes");

            migrationBuilder.DropIndex(
                name: "IX_Inspecoes_VeiculoId",
                table: "Inspecoes");

            migrationBuilder.DropColumn(
                name: "ImagemCadastroCriptografada",
                table: "TemplatesBiometricoFutronic");

            migrationBuilder.DropColumn(
                name: "DispositivoAgenteId",
                table: "DocumentoSignatarios");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "DocumentoSignatarios");

            migrationBuilder.DropColumn(
                name: "LocalizacaoStatus",
                table: "DocumentoSignatarios");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "DocumentoSignatarios");

            migrationBuilder.DropColumn(
                name: "PrecisaoMetros",
                table: "DocumentoSignatarios");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "DocumentoSignatarios");

            migrationBuilder.DropColumn(
                name: "ValidacaoGrupoId",
                table: "DocumentoSignatarios");

            migrationBuilder.DropColumn(
                name: "ValidacaoModelo",
                table: "DocumentoSignatarios");

            migrationBuilder.DropColumn(
                name: "ValidacaoRequisicaoId",
                table: "DocumentoSignatarios");

            migrationBuilder.CreateIndex(
                name: "IX_Inspecoes_AlojamentoId",
                table: "Inspecoes",
                column: "AlojamentoId",
                unique: true,
                filter: "[AlojamentoId] IS NOT NULL AND [Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Inspecoes_VeiculoId",
                table: "Inspecoes",
                column: "VeiculoId",
                unique: true,
                filter: "[VeiculoId] IS NOT NULL AND [Status] = 1");
        }
    }
}
