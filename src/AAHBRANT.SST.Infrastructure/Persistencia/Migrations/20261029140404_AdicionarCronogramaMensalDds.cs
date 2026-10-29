using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCronogramaMensalDds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CronogramasMensaisDds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ano = table.Column<int>(type: "int", nullable: false),
                    Mes = table.Column<int>(type: "int", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Versao = table.Column<int>(type: "int", nullable: false),
                    ProgramacaoJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResponsavelUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CronogramasMensaisDds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CronogramasMensaisDds_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CronogramasMensaisDds_Usuarios_ResponsavelUsuarioId",
                        column: x => x.ResponsavelUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CronogramasMensaisDdsRevisoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CronogramaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    VersaoOrigem = table.Column<int>(type: "int", nullable: false),
                    HashPlanejamento = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ConteudoJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DocumentoAssinaturaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CronogramasMensaisDdsRevisoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CronogramasMensaisDdsRevisoes_CronogramasMensaisDds_CronogramaId",
                        column: x => x.CronogramaId,
                        principalTable: "CronogramasMensaisDds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CronogramasMensaisDdsRevisoes_DocumentosAssinatura_DocumentoAssinaturaId",
                        column: x => x.DocumentoAssinaturaId,
                        principalTable: "DocumentosAssinatura",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CronogramasMensaisDds_ObraId_Ano_Mes",
                table: "CronogramasMensaisDds",
                columns: new[] { "ObraId", "Ano", "Mes" },
                unique: true,
                filter: "[Ativo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CronogramasMensaisDds_ResponsavelUsuarioId",
                table: "CronogramasMensaisDds",
                column: "ResponsavelUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_CronogramasMensaisDdsRevisoes_CronogramaId_HashPlanejamento",
                table: "CronogramasMensaisDdsRevisoes",
                columns: new[] { "CronogramaId", "HashPlanejamento" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CronogramasMensaisDdsRevisoes_CronogramaId_Numero",
                table: "CronogramasMensaisDdsRevisoes",
                columns: new[] { "CronogramaId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CronogramasMensaisDdsRevisoes_DocumentoAssinaturaId",
                table: "CronogramasMensaisDdsRevisoes",
                column: "DocumentoAssinaturaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CronogramasMensaisDdsRevisoes");

            migrationBuilder.DropTable(
                name: "CronogramasMensaisDds");
        }
    }
}
