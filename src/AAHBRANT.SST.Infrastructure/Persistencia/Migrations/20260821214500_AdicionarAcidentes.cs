using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarAcidentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Acidentes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AtividadeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Local = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Data = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Hora = table.Column<TimeSpan>(type: "time", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Lesao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Consequencia = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Atendimento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HouveAfastamento = table.Column<bool>(type: "bit", nullable: false),
                    DiasAfastamento = table.Column<int>(type: "int", nullable: true),
                    NumeroCat = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MetodologiaInvestigacao = table.Column<int>(type: "int", nullable: true),
                    Causas = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DataConclusaoInvestigacao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acidentes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acidentes_Atividades_AtividadeId",
                        column: x => x.AtividadeId,
                        principalTable: "Atividades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Acidentes_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Acidentes_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acidentes_AtividadeId",
                table: "Acidentes",
                column: "AtividadeId");

            migrationBuilder.CreateIndex(
                name: "IX_Acidentes_ObraId",
                table: "Acidentes",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_Acidentes_Status",
                table: "Acidentes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Acidentes_Tipo",
                table: "Acidentes",
                column: "Tipo");

            migrationBuilder.CreateIndex(
                name: "IX_Acidentes_TrabalhadorId",
                table: "Acidentes",
                column: "TrabalhadorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acidentes");
        }
    }
}
