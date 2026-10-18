using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CriarPortalTerceiros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AcessosPortalTerceiro",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContratoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiraEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevogadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CodigoHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CodigoExpiraEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CodigoEnviadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TentativasCodigo = table.Column<int>(type: "int", nullable: false),
                    SessaoHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SessaoExpiraEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimoAcessoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TermoAceitoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TermoVersaoAceita = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
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
                    table.PrimaryKey("PK_AcessosPortalTerceiro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcessosPortalTerceiro_Contratos_ContratoId",
                        column: x => x.ContratoId,
                        principalTable: "Contratos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentosEnviadosTerceiro",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcessoPortalTerceiroId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContratoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    NomeArquivo = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Conteudo = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    TamanhoBytes = table.Column<long>(type: "bigint", nullable: false),
                    HashSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DataDocumento = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataValidade = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TipoExameAso = table.Column<int>(type: "int", nullable: true),
                    ResultadoAso = table.Column<int>(type: "int", nullable: true),
                    CursoTreinamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CargaHoraria = table.Column<int>(type: "int", nullable: true),
                    InstituicaoInstrutor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NumeroCertificado = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MotivoRecusa = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ValidadoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ValidadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AsoGeradoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TreinamentoGeradoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_DocumentosEnviadosTerceiro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentosEnviadosTerceiro_AcessosPortalTerceiro_AcessoPortalTerceiroId",
                        column: x => x.AcessoPortalTerceiroId,
                        principalTable: "AcessosPortalTerceiro",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentosEnviadosTerceiro_Contratos_ContratoId",
                        column: x => x.ContratoId,
                        principalTable: "Contratos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentosEnviadosTerceiro_CursosTreinamento_CursoTreinamentoId",
                        column: x => x.CursoTreinamentoId,
                        principalTable: "CursosTreinamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentosEnviadosTerceiro_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcessosPortalTerceiro_ContratoId",
                table: "AcessosPortalTerceiro",
                column: "ContratoId");

            migrationBuilder.CreateIndex(
                name: "IX_AcessosPortalTerceiro_SessaoHash",
                table: "AcessosPortalTerceiro",
                column: "SessaoHash",
                filter: "[SessaoHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AcessosPortalTerceiro_TokenHash",
                table: "AcessosPortalTerceiro",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosEnviadosTerceiro_AcessoPortalTerceiroId",
                table: "DocumentosEnviadosTerceiro",
                column: "AcessoPortalTerceiroId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosEnviadosTerceiro_ContratoId_Status",
                table: "DocumentosEnviadosTerceiro",
                columns: new[] { "ContratoId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosEnviadosTerceiro_CursoTreinamentoId",
                table: "DocumentosEnviadosTerceiro",
                column: "CursoTreinamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosEnviadosTerceiro_TrabalhadorId",
                table: "DocumentosEnviadosTerceiro",
                column: "TrabalhadorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentosEnviadosTerceiro");

            migrationBuilder.DropTable(
                name: "AcessosPortalTerceiro");
        }
    }
}
