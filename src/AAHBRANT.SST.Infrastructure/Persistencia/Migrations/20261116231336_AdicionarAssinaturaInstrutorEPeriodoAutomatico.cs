using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarAssinaturaInstrutorEPeriodoAutomatico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Automatico",
                table: "PeriodosEtapaIntegracao",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AssinaturasInstrutorSessaoTreinamento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessaoTreinamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstrutorTrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Metodo = table.Column<int>(type: "int", nullable: false),
                    AssinadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScoreConfianca = table.Column<double>(type: "float", nullable: true),
                    EvidenciaConteudo = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    EvidenciaContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EvidenciaHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    LocalizacaoStatus = table.Column<int>(type: "int", nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    PrecisaoMetros = table.Column<double>(type: "float", nullable: true),
                    DispositivoAgenteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ValidacaoModelo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ValidacaoGrupoId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ValidacaoRequisicaoId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
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
                    table.PrimaryKey("PK_AssinaturasInstrutorSessaoTreinamento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssinaturasInstrutorSessaoTreinamento_SessoesTreinamento_SessaoTreinamentoId",
                        column: x => x.SessaoTreinamentoId,
                        principalTable: "SessoesTreinamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssinaturasInstrutorSessaoTreinamento_Trabalhadores_InstrutorTrabalhadorId",
                        column: x => x.InstrutorTrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssinaturasInstrutorSessaoTreinamento_InstrutorTrabalhadorId",
                table: "AssinaturasInstrutorSessaoTreinamento",
                column: "InstrutorTrabalhadorId");

            migrationBuilder.CreateIndex(
                name: "IX_AssinaturasInstrutorSessaoTreinamento_SessaoTreinamentoId",
                table: "AssinaturasInstrutorSessaoTreinamento",
                column: "SessaoTreinamentoId",
                unique: true,
                filter: "[Ativo] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssinaturasInstrutorSessaoTreinamento");

            migrationBuilder.DropColumn(
                name: "Automatico",
                table: "PeriodosEtapaIntegracao");
        }
    }
}
