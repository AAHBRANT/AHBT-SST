using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class GheEExamesFuncaoObra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GheId",
                table: "Atividades",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExamesFuncaoObra",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FuncaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PcmsoDetalheId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Exame = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CodigoExame = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Admissional = table.Column<bool>(type: "bit", nullable: false),
                    Periodico = table.Column<bool>(type: "bit", nullable: false),
                    RetornoTrabalho = table.Column<bool>(type: "bit", nullable: false),
                    MudancaRisco = table.Column<bool>(type: "bit", nullable: false),
                    Demissional = table.Column<bool>(type: "bit", nullable: false),
                    PeriodicidadeMeses = table.Column<int>(type: "int", nullable: true),
                    Observacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_ExamesFuncaoObra", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamesFuncaoObra_Funcoes_FuncaoId",
                        column: x => x.FuncaoId,
                        principalTable: "Funcoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamesFuncaoObra_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamesFuncaoObra_PcmsoDetalhes_PcmsoDetalheId",
                        column: x => x.PcmsoDetalheId,
                        principalTable: "PcmsoDetalhes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Ghes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Setor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    JornadaTrabalho = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DescricaoAmbiente = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AtividadesCriticas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FonteGeradora = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MedidasProtecaoExistentes = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Ghes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ghes_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GheFuncoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GheId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FuncaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuantidadeExpostos = table.Column<int>(type: "int", nullable: true),
                    DescricaoAtividades = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_GheFuncoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GheFuncoes_Funcoes_FuncaoId",
                        column: x => x.FuncaoId,
                        principalTable: "Funcoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GheFuncoes_Ghes_GheId",
                        column: x => x.GheId,
                        principalTable: "Ghes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Atividades_GheId",
                table: "Atividades",
                column: "GheId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamesFuncaoObra_FuncaoId",
                table: "ExamesFuncaoObra",
                column: "FuncaoId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamesFuncaoObra_ObraId_FuncaoId",
                table: "ExamesFuncaoObra",
                columns: new[] { "ObraId", "FuncaoId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamesFuncaoObra_PcmsoDetalheId",
                table: "ExamesFuncaoObra",
                column: "PcmsoDetalheId");

            migrationBuilder.CreateIndex(
                name: "IX_GheFuncoes_FuncaoId",
                table: "GheFuncoes",
                column: "FuncaoId");

            migrationBuilder.CreateIndex(
                name: "IX_GheFuncoes_GheId_FuncaoId",
                table: "GheFuncoes",
                columns: new[] { "GheId", "FuncaoId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ghes_ObraId_Numero",
                table: "Ghes",
                columns: new[] { "ObraId", "Numero" });

            migrationBuilder.AddForeignKey(
                name: "FK_Atividades_Ghes_GheId",
                table: "Atividades",
                column: "GheId",
                principalTable: "Ghes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Atividades_Ghes_GheId",
                table: "Atividades");

            migrationBuilder.DropTable(
                name: "ExamesFuncaoObra");

            migrationBuilder.DropTable(
                name: "GheFuncoes");

            migrationBuilder.DropTable(
                name: "Ghes");

            migrationBuilder.DropIndex(
                name: "IX_Atividades_GheId",
                table: "Atividades");

            migrationBuilder.DropColumn(
                name: "GheId",
                table: "Atividades");
        }
    }
}
