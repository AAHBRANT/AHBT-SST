using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarRelatoriosAgendados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EncerradoEm",
                table: "Dds",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DestinatariosRelatorio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ListaPresenca = table.Column<bool>(type: "bit", nullable: false),
                    BoletimSemanal = table.Column<bool>(type: "bit", nullable: false),
                    Ocorrencia = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_DestinatariosRelatorio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DestinatariosRelatorio_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DestinatariosRelatorio_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExecucoesRelatorioAgendado",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<DateTime>(type: "date", nullable: false),
                    ExecutadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RelatoriosGerados = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ExecucoesRelatorioAgendado", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RelatoriosGerados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChaveUnica = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Resumo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Imagem = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Pdf = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    PdfNome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReferenciaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PeriodoInicio = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PeriodoFim = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GeradoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_RelatoriosGerados", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RelatorioEnvios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelatorioGeradoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Canal = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Sucesso = table.Column<bool>(type: "bit", nullable: false),
                    Erro = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_RelatorioEnvios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RelatorioEnvios_RelatoriosGerados_RelatorioGeradoId",
                        column: x => x.RelatorioGeradoId,
                        principalTable: "RelatoriosGerados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DestinatariosRelatorio_ObraId",
                table: "DestinatariosRelatorio",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_DestinatariosRelatorio_UsuarioId_ObraId",
                table: "DestinatariosRelatorio",
                columns: new[] { "UsuarioId", "ObraId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecucoesRelatorioAgendado_Tipo_Data",
                table: "ExecucoesRelatorioAgendado",
                columns: new[] { "Tipo", "Data" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RelatorioEnvios_RelatorioGeradoId",
                table: "RelatorioEnvios",
                column: "RelatorioGeradoId");

            migrationBuilder.CreateIndex(
                name: "IX_RelatoriosGerados_ChaveUnica",
                table: "RelatoriosGerados",
                column: "ChaveUnica",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RelatoriosGerados_ObraId_GeradoEm",
                table: "RelatoriosGerados",
                columns: new[] { "ObraId", "GeradoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_RelatoriosGerados_Tipo_GeradoEm",
                table: "RelatoriosGerados",
                columns: new[] { "Tipo", "GeradoEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DestinatariosRelatorio");

            migrationBuilder.DropTable(
                name: "ExecucoesRelatorioAgendado");

            migrationBuilder.DropTable(
                name: "RelatorioEnvios");

            migrationBuilder.DropTable(
                name: "RelatoriosGerados");

            migrationBuilder.DropColumn(
                name: "EncerradoEm",
                table: "Dds");
        }
    }
}
