using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarSessaoTreinamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SessaoTreinamentoId",
                table: "Treinamentos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SessoesTreinamento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CursoTreinamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataRealizacao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CargaHorariaRealizada = table.Column<int>(type: "int", nullable: false),
                    InstituicaoInstrutor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NumeroCertificado = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DataEncerramento = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_SessoesTreinamento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessoesTreinamento_CursosTreinamento_CursoTreinamentoId",
                        column: x => x.CursoTreinamentoId,
                        principalTable: "CursosTreinamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SessoesTreinamento_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FotosEvidenciaSessaoTreinamento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessaoTreinamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    FotoConteudo = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    FotoContentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_FotosEvidenciaSessaoTreinamento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FotosEvidenciaSessaoTreinamento_SessoesTreinamento_SessaoTreinamentoId",
                        column: x => x.SessaoTreinamentoId,
                        principalTable: "SessoesTreinamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParticipantesSessaoTreinamento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessaoTreinamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PresencaConfirmadaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScoreConfianca = table.Column<double>(type: "float", nullable: true),
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
                    table.PrimaryKey("PK_ParticipantesSessaoTreinamento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParticipantesSessaoTreinamento_SessoesTreinamento_SessaoTreinamentoId",
                        column: x => x.SessaoTreinamentoId,
                        principalTable: "SessoesTreinamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParticipantesSessaoTreinamento_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParticipantesSessaoTreinamento_Treinamentos_TreinamentoGeradoId",
                        column: x => x.TreinamentoGeradoId,
                        principalTable: "Treinamentos",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Treinamentos_SessaoTreinamentoId",
                table: "Treinamentos",
                column: "SessaoTreinamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_FotosEvidenciaSessaoTreinamento_SessaoTreinamentoId",
                table: "FotosEvidenciaSessaoTreinamento",
                column: "SessaoTreinamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantesSessaoTreinamento_SessaoTreinamentoId",
                table: "ParticipantesSessaoTreinamento",
                column: "SessaoTreinamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantesSessaoTreinamento_TrabalhadorId",
                table: "ParticipantesSessaoTreinamento",
                column: "TrabalhadorId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantesSessaoTreinamento_TreinamentoGeradoId",
                table: "ParticipantesSessaoTreinamento",
                column: "TreinamentoGeradoId");

            migrationBuilder.CreateIndex(
                name: "IX_SessoesTreinamento_CursoTreinamentoId",
                table: "SessoesTreinamento",
                column: "CursoTreinamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_SessoesTreinamento_ObraId",
                table: "SessoesTreinamento",
                column: "ObraId");

            migrationBuilder.AddForeignKey(
                name: "FK_Treinamentos_SessoesTreinamento_SessaoTreinamentoId",
                table: "Treinamentos",
                column: "SessaoTreinamentoId",
                principalTable: "SessoesTreinamento",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Treinamentos_SessoesTreinamento_SessaoTreinamentoId",
                table: "Treinamentos");

            migrationBuilder.DropTable(
                name: "FotosEvidenciaSessaoTreinamento");

            migrationBuilder.DropTable(
                name: "ParticipantesSessaoTreinamento");

            migrationBuilder.DropTable(
                name: "SessoesTreinamento");

            migrationBuilder.DropIndex(
                name: "IX_Treinamentos_SessaoTreinamentoId",
                table: "Treinamentos");

            migrationBuilder.DropColumn(
                name: "SessaoTreinamentoId",
                table: "Treinamentos");
        }
    }
}
