using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarNaoAssinouETokenCapturaFoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NaoAssinouEtapaIntegracao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntegracaoEtapaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    MarcadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MarcadoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_NaoAssinouEtapaIntegracao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NaoAssinouEtapaIntegracao_IntegracaoEtapas_IntegracaoEtapaId",
                        column: x => x.IntegracaoEtapaId,
                        principalTable: "IntegracaoEtapas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NaoAssinouEtapaIntegracao_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TokensCapturaFoto",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessaoTreinamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Token = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiraEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_TokensCapturaFoto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TokensCapturaFoto_SessoesTreinamento_SessaoTreinamentoId",
                        column: x => x.SessaoTreinamentoId,
                        principalTable: "SessoesTreinamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NaoAssinouEtapaIntegracao_IntegracaoEtapaId_TrabalhadorId",
                table: "NaoAssinouEtapaIntegracao",
                columns: new[] { "IntegracaoEtapaId", "TrabalhadorId" },
                unique: true,
                filter: "[Ativo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_NaoAssinouEtapaIntegracao_TrabalhadorId",
                table: "NaoAssinouEtapaIntegracao",
                column: "TrabalhadorId");

            migrationBuilder.CreateIndex(
                name: "IX_TokensCapturaFoto_SessaoTreinamentoId",
                table: "TokensCapturaFoto",
                column: "SessaoTreinamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_TokensCapturaFoto_Token",
                table: "TokensCapturaFoto",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NaoAssinouEtapaIntegracao");

            migrationBuilder.DropTable(
                name: "TokensCapturaFoto");
        }
    }
}
