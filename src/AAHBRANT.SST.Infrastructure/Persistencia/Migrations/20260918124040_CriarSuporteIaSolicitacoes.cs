using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CriarSuporteIaSolicitacoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SuporteIaSolicitacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    SeveridadeInformada = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Modulo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UrlContexto = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: true),
                    SolicitanteUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SolicitanteNome = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    SolicitanteEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ResultadoTriagem = table.Column<int>(type: "int", nullable: false),
                    RequerAlteracaoCodigo = table.Column<bool>(type: "bit", nullable: false),
                    RespostaAoUsuario = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DemandaReduzida = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SolucaoProposta = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    EvidenciasTecnicas = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EncaminhadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AlertaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_SuporteIaSolicitacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SuporteIaSolicitacoes_Alertas_AlertaId",
                        column: x => x.AlertaId,
                        principalTable: "Alertas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SuporteIaSolicitacoes_Usuarios_SolicitanteUsuarioId",
                        column: x => x.SolicitanteUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SuporteIaSolicitacoes_AlertaId",
                table: "SuporteIaSolicitacoes",
                column: "AlertaId");

            migrationBuilder.CreateIndex(
                name: "IX_SuporteIaSolicitacoes_ResultadoTriagem",
                table: "SuporteIaSolicitacoes",
                column: "ResultadoTriagem");

            migrationBuilder.CreateIndex(
                name: "IX_SuporteIaSolicitacoes_SolicitanteUsuarioId",
                table: "SuporteIaSolicitacoes",
                column: "SolicitanteUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_SuporteIaSolicitacoes_Status",
                table: "SuporteIaSolicitacoes",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SuporteIaSolicitacoes");
        }
    }
}
