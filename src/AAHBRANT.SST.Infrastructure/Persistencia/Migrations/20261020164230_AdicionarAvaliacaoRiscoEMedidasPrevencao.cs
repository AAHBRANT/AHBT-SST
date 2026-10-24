using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarAvaliacaoRiscoEMedidasPrevencao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AvaliadoEmUtc",
                table: "Riscos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvaliadoPorNome",
                table: "Riscos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AvaliadoPorUsuarioId",
                table: "Riscos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MedidasPrevencao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiscoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fase = table.Column<int>(type: "int", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OrigemMedida = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    ValidadoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ValidadoPorNome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ValidadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_MedidasPrevencao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedidasPrevencao_Riscos_RiscoId",
                        column: x => x.RiscoId,
                        principalTable: "Riscos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MedidasPrevencao_RiscoId_Fase_Ordem",
                table: "MedidasPrevencao",
                columns: new[] { "RiscoId", "Fase", "Ordem" });

            // Riscos que já vieram da leitura do PGR (atividade ligada a um GHE) foram avaliados no
            // próprio PGR, aprovado pelo engenheiro: contam como avaliação confirmada.
            migrationBuilder.Sql("""
                UPDATE r SET r.AvaliadoEmUtc = r.CreatedAtUtc, r.AvaliadoPorNome = N'Importado do PGR'
                FROM Riscos r
                INNER JOIN Atividades a ON a.Id = r.AtividadeId
                WHERE a.GheId IS NOT NULL AND r.AvaliadoEmUtc IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MedidasPrevencao");

            migrationBuilder.DropColumn(
                name: "AvaliadoEmUtc",
                table: "Riscos");

            migrationBuilder.DropColumn(
                name: "AvaliadoPorNome",
                table: "Riscos");

            migrationBuilder.DropColumn(
                name: "AvaliadoPorUsuarioId",
                table: "Riscos");
        }
    }
}
