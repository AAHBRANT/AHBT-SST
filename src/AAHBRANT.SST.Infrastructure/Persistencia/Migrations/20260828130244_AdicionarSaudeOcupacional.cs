using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarSaudeOcupacional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AptidoesAtividadeEspecifica",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AtividadeCritica = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Aptidao = table.Column<int>(type: "int", nullable: false),
                    DataAvaliacao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataValidade = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MedicoResponsavel = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Observacoes = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_AptidoesAtividadeEspecifica", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AptidoesAtividadeEspecifica_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamesComplementares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AsoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    DataRealizacao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataValidade = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Resultado = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Observacoes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponsavelTecnico = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
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
                    table.PrimaryKey("PK_ExamesComplementares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamesComplementares_Asos_AsoId",
                        column: x => x.AsoId,
                        principalTable: "Asos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamesComplementares_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PcmsoDetalhes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentoGestaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MedicoResponsavelNome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    MedicoResponsavelCrm = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FuncoesContempladas = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RiscosConsiderados = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExamesPrevistos = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Periodicidades = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnidadesObrasAbrangidas = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_PcmsoDetalhes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PcmsoDetalhes_DocumentosGestao_DocumentoGestaoId",
                        column: x => x.DocumentoGestaoId,
                        principalTable: "DocumentosGestao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AptidoesAtividadeEspecifica_TrabalhadorId_DataValidade",
                table: "AptidoesAtividadeEspecifica",
                columns: new[] { "TrabalhadorId", "DataValidade" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamesComplementares_AsoId",
                table: "ExamesComplementares",
                column: "AsoId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamesComplementares_TrabalhadorId_DataValidade",
                table: "ExamesComplementares",
                columns: new[] { "TrabalhadorId", "DataValidade" });

            migrationBuilder.CreateIndex(
                name: "IX_PcmsoDetalhes_DocumentoGestaoId",
                table: "PcmsoDetalhes",
                column: "DocumentoGestaoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AptidoesAtividadeEspecifica");

            migrationBuilder.DropTable(
                name: "ExamesComplementares");

            migrationBuilder.DropTable(
                name: "PcmsoDetalhes");
        }
    }
}
