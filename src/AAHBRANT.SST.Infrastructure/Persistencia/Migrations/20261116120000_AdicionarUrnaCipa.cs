using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarUrnaCipa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TotalEleitoresAptos",
                table: "ProcessosEleitoraisCipa",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VotacaoAbertaEmUtc",
                table: "ProcessosEleitoraisCipa",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VotacaoEncerradaEmUtc",
                table: "ProcessosEleitoraisCipa",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EleitoresCipa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessoEleitoralId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdentificadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VotouEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metodo = table.Column<int>(type: "int", nullable: false),
                    LiberadoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_EleitoresCipa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EleitoresCipa_ProcessosEleitoraisCipa_ProcessoEleitoralId",
                        column: x => x.ProcessoEleitoralId,
                        principalTable: "ProcessosEleitoraisCipa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EleitoresCipa_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VotosCipa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessoEleitoralId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CandidatoCipaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VotosCipa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VotosCipa_CandidatosCipa_CandidatoCipaId",
                        column: x => x.CandidatoCipaId,
                        principalTable: "CandidatosCipa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VotosCipa_ProcessosEleitoraisCipa_ProcessoEleitoralId",
                        column: x => x.ProcessoEleitoralId,
                        principalTable: "ProcessosEleitoraisCipa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EleitoresCipa_ProcessoEleitoralId_TrabalhadorId",
                table: "EleitoresCipa",
                columns: new[] { "ProcessoEleitoralId", "TrabalhadorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EleitoresCipa_TrabalhadorId",
                table: "EleitoresCipa",
                column: "TrabalhadorId");

            migrationBuilder.CreateIndex(
                name: "IX_VotosCipa_CandidatoCipaId",
                table: "VotosCipa",
                column: "CandidatoCipaId");

            migrationBuilder.CreateIndex(
                name: "IX_VotosCipa_ProcessoEleitoralId",
                table: "VotosCipa",
                column: "ProcessoEleitoralId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "EleitoresCipa");
            migrationBuilder.DropTable(name: "VotosCipa");
            migrationBuilder.DropColumn(name: "TotalEleitoresAptos", table: "ProcessosEleitoraisCipa");
            migrationBuilder.DropColumn(name: "VotacaoAbertaEmUtc", table: "ProcessosEleitoraisCipa");
            migrationBuilder.DropColumn(name: "VotacaoEncerradaEmUtc", table: "ProcessosEleitoraisCipa");
        }
    }
}
