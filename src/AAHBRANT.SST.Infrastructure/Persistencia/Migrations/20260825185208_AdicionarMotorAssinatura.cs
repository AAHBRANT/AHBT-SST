using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarMotorAssinatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TrabalhadorId",
                table: "TrilhaAuditoria",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsentimentoBiometriaEm",
                table: "Trabalhadores",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PinHash",
                table: "Trabalhadores",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TermoAceiteAssinaturaEletronicaEm",
                table: "Trabalhadores",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MetodosAutenticacaoHabilitados",
                table: "Obras",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DocumentosAssinatura",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntidadeTipo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntidadeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ConteudoHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TokenValidacaoPublica = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FinalizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PdfConteudo = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentosAssinatura", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentoSignatarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentoAssinaturaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MetodoAutenticacao = table.Column<int>(type: "int", nullable: false),
                    AssinadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentoSignatarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentoSignatarios_DocumentosAssinatura_DocumentoAssinaturaId",
                        column: x => x.DocumentoAssinaturaId,
                        principalTable: "DocumentosAssinatura",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentoSignatarios_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrilhaAuditoria_TrabalhadorId",
                table: "TrilhaAuditoria",
                column: "TrabalhadorId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosAssinatura_EntidadeTipo_EntidadeId",
                table: "DocumentosAssinatura",
                columns: new[] { "EntidadeTipo", "EntidadeId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosAssinatura_TokenValidacaoPublica",
                table: "DocumentosAssinatura",
                column: "TokenValidacaoPublica",
                unique: true,
                filter: "[TokenValidacaoPublica] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoSignatarios_DocumentoAssinaturaId_TrabalhadorId",
                table: "DocumentoSignatarios",
                columns: new[] { "DocumentoAssinaturaId", "TrabalhadorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoSignatarios_TrabalhadorId",
                table: "DocumentoSignatarios",
                column: "TrabalhadorId");

            migrationBuilder.AddForeignKey(
                name: "FK_TrilhaAuditoria_Trabalhadores_TrabalhadorId",
                table: "TrilhaAuditoria",
                column: "TrabalhadorId",
                principalTable: "Trabalhadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TrilhaAuditoria_Trabalhadores_TrabalhadorId",
                table: "TrilhaAuditoria");

            migrationBuilder.DropTable(
                name: "DocumentoSignatarios");

            migrationBuilder.DropTable(
                name: "DocumentosAssinatura");

            migrationBuilder.DropIndex(
                name: "IX_TrilhaAuditoria_TrabalhadorId",
                table: "TrilhaAuditoria");

            migrationBuilder.DropColumn(
                name: "TrabalhadorId",
                table: "TrilhaAuditoria");

            migrationBuilder.DropColumn(
                name: "ConsentimentoBiometriaEm",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "PinHash",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "TermoAceiteAssinaturaEletronicaEm",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "MetodosAutenticacaoHabilitados",
                table: "Obras");
        }
    }
}
