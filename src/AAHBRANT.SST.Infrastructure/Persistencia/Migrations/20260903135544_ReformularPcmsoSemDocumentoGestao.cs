using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class ReformularPcmsoSemDocumentoGestao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PcmsoDetalhes_DocumentoGestaoId",
                table: "PcmsoDetalhes");

            migrationBuilder.DropColumn(
                name: "DocumentoGestaoId",
                table: "PcmsoDetalhes");

            migrationBuilder.AddColumn<string>(
                name: "Arquivo",
                table: "PcmsoDetalhes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataEmissao",
                table: "PcmsoDetalhes",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Nome",
                table: "PcmsoDetalhes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ObraId",
                table: "PcmsoDetalhes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResponsavelUsuarioId",
                table: "PcmsoDetalhes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SetorId",
                table: "PcmsoDetalhes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "PcmsoDetalhes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "Validade",
                table: "PcmsoDetalhes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Versao",
                table: "PcmsoDetalhes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PcmsoDetalhes_ObraId",
                table: "PcmsoDetalhes",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_PcmsoDetalhes_ResponsavelUsuarioId",
                table: "PcmsoDetalhes",
                column: "ResponsavelUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_PcmsoDetalhes_SetorId",
                table: "PcmsoDetalhes",
                column: "SetorId");

            migrationBuilder.AddForeignKey(
                name: "FK_PcmsoDetalhes_Obras_ObraId",
                table: "PcmsoDetalhes",
                column: "ObraId",
                principalTable: "Obras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PcmsoDetalhes_Setores_SetorId",
                table: "PcmsoDetalhes",
                column: "SetorId",
                principalTable: "Setores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PcmsoDetalhes_Usuarios_ResponsavelUsuarioId",
                table: "PcmsoDetalhes",
                column: "ResponsavelUsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PcmsoDetalhes_Obras_ObraId",
                table: "PcmsoDetalhes");

            migrationBuilder.DropForeignKey(
                name: "FK_PcmsoDetalhes_Setores_SetorId",
                table: "PcmsoDetalhes");

            migrationBuilder.DropForeignKey(
                name: "FK_PcmsoDetalhes_Usuarios_ResponsavelUsuarioId",
                table: "PcmsoDetalhes");

            migrationBuilder.DropIndex(
                name: "IX_PcmsoDetalhes_ObraId",
                table: "PcmsoDetalhes");

            migrationBuilder.DropIndex(
                name: "IX_PcmsoDetalhes_ResponsavelUsuarioId",
                table: "PcmsoDetalhes");

            migrationBuilder.DropIndex(
                name: "IX_PcmsoDetalhes_SetorId",
                table: "PcmsoDetalhes");

            migrationBuilder.DropColumn(
                name: "Arquivo",
                table: "PcmsoDetalhes");

            migrationBuilder.DropColumn(
                name: "DataEmissao",
                table: "PcmsoDetalhes");

            migrationBuilder.DropColumn(
                name: "Nome",
                table: "PcmsoDetalhes");

            migrationBuilder.DropColumn(
                name: "ObraId",
                table: "PcmsoDetalhes");

            migrationBuilder.DropColumn(
                name: "ResponsavelUsuarioId",
                table: "PcmsoDetalhes");

            migrationBuilder.DropColumn(
                name: "SetorId",
                table: "PcmsoDetalhes");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PcmsoDetalhes");

            migrationBuilder.DropColumn(
                name: "Validade",
                table: "PcmsoDetalhes");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "PcmsoDetalhes");

            migrationBuilder.AddColumn<Guid>(
                name: "DocumentoGestaoId",
                table: "PcmsoDetalhes",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_PcmsoDetalhes_DocumentoGestaoId",
                table: "PcmsoDetalhes",
                column: "DocumentoGestaoId",
                unique: true);
        }
    }
}
