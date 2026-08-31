using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class ReformularAprLiteralREV02 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AprEtapaRiscos_Riscos_RiscoId",
                table: "AprEtapaRiscos");

            migrationBuilder.DropIndex(
                name: "IX_AprEtapaRiscos_AprEtapaId_RiscoId",
                table: "AprEtapaRiscos");

            migrationBuilder.DropIndex(
                name: "IX_AprEtapaRiscos_RiscoId",
                table: "AprEtapaRiscos");

            migrationBuilder.DropColumn(
                name: "MedidasPreventivas",
                table: "AprEtapas");

            migrationBuilder.DropColumn(
                name: "RiscoId",
                table: "AprEtapaRiscos");

            migrationBuilder.AddColumn<string>(
                name: "MaquinasEquipamentos",
                table: "Aprs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroApr",
                table: "Aprs",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PgrReferencia",
                table: "Aprs",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FonteCircunstancia",
                table: "AprEtapaRiscos",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MedidasPrevencao",
                table: "AprEtapaRiscos",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NivelRiscoInicial",
                table: "AprEtapaRiscos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NivelRiscoResidual",
                table: "AprEtapaRiscos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PerigoEventoPerigoso",
                table: "AprEtapaRiscos",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PossiveisLesoes",
                table: "AprEtapaRiscos",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProbabilidadeInicial",
                table: "AprEtapaRiscos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProbabilidadeResidual",
                table: "AprEtapaRiscos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Responsavel",
                table: "AprEtapaRiscos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeveridadeInicial",
                table: "AprEtapaRiscos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SeveridadeResidual",
                table: "AprEtapaRiscos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TrabalhadoresExpostos",
                table: "AprEtapaRiscos",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AprEtapaRiscos_AprEtapaId",
                table: "AprEtapaRiscos",
                column: "AprEtapaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AprEtapaRiscos_AprEtapaId",
                table: "AprEtapaRiscos");

            migrationBuilder.DropColumn(
                name: "MaquinasEquipamentos",
                table: "Aprs");

            migrationBuilder.DropColumn(
                name: "NumeroApr",
                table: "Aprs");

            migrationBuilder.DropColumn(
                name: "PgrReferencia",
                table: "Aprs");

            migrationBuilder.DropColumn(
                name: "FonteCircunstancia",
                table: "AprEtapaRiscos");

            migrationBuilder.DropColumn(
                name: "MedidasPrevencao",
                table: "AprEtapaRiscos");

            migrationBuilder.DropColumn(
                name: "NivelRiscoInicial",
                table: "AprEtapaRiscos");

            migrationBuilder.DropColumn(
                name: "NivelRiscoResidual",
                table: "AprEtapaRiscos");

            migrationBuilder.DropColumn(
                name: "PerigoEventoPerigoso",
                table: "AprEtapaRiscos");

            migrationBuilder.DropColumn(
                name: "PossiveisLesoes",
                table: "AprEtapaRiscos");

            migrationBuilder.DropColumn(
                name: "ProbabilidadeInicial",
                table: "AprEtapaRiscos");

            migrationBuilder.DropColumn(
                name: "ProbabilidadeResidual",
                table: "AprEtapaRiscos");

            migrationBuilder.DropColumn(
                name: "Responsavel",
                table: "AprEtapaRiscos");

            migrationBuilder.DropColumn(
                name: "SeveridadeInicial",
                table: "AprEtapaRiscos");

            migrationBuilder.DropColumn(
                name: "SeveridadeResidual",
                table: "AprEtapaRiscos");

            migrationBuilder.DropColumn(
                name: "TrabalhadoresExpostos",
                table: "AprEtapaRiscos");

            migrationBuilder.AddColumn<string>(
                name: "MedidasPreventivas",
                table: "AprEtapas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RiscoId",
                table: "AprEtapaRiscos",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_AprEtapaRiscos_AprEtapaId_RiscoId",
                table: "AprEtapaRiscos",
                columns: new[] { "AprEtapaId", "RiscoId" });

            migrationBuilder.CreateIndex(
                name: "IX_AprEtapaRiscos_RiscoId",
                table: "AprEtapaRiscos",
                column: "RiscoId");

            migrationBuilder.AddForeignKey(
                name: "FK_AprEtapaRiscos_Riscos_RiscoId",
                table: "AprEtapaRiscos",
                column: "RiscoId",
                principalTable: "Riscos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
