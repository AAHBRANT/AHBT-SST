using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarVinculoTerceirizadoEmTrabalhador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Trabalhadores_Contratos_ContratoId",
                table: "Trabalhadores");

            migrationBuilder.AddColumn<Guid>(
                name: "EmpresaId",
                table: "Trabalhadores",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trabalhadores_EmpresaId",
                table: "Trabalhadores",
                column: "EmpresaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Trabalhadores_Contratos_ContratoId",
                table: "Trabalhadores",
                column: "ContratoId",
                principalTable: "Contratos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Trabalhadores_Empresas_EmpresaId",
                table: "Trabalhadores",
                column: "EmpresaId",
                principalTable: "Empresas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Trabalhadores_Contratos_ContratoId",
                table: "Trabalhadores");

            migrationBuilder.DropForeignKey(
                name: "FK_Trabalhadores_Empresas_EmpresaId",
                table: "Trabalhadores");

            migrationBuilder.DropIndex(
                name: "IX_Trabalhadores_EmpresaId",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "Trabalhadores");

            migrationBuilder.AddForeignKey(
                name: "FK_Trabalhadores_Contratos_ContratoId",
                table: "Trabalhadores",
                column: "ContratoId",
                principalTable: "Contratos",
                principalColumn: "Id");
        }
    }
}
