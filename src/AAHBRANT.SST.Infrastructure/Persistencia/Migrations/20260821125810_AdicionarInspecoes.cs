using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarInspecoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChecklistModelos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TipoInspecao = table.Column<int>(type: "int", nullable: false),
                    Versao = table.Column<int>(type: "int", nullable: false),
                    ChecklistModeloAnteriorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_ChecklistModelos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChecklistModelos_ChecklistModelos_ChecklistModeloAnteriorId",
                        column: x => x.ChecklistModeloAnteriorId,
                        principalTable: "ChecklistModelos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChecklistModeloItens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChecklistModeloId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExigeFotografia = table.Column<bool>(type: "bit", nullable: false),
                    ExigeResponsavel = table.Column<bool>(type: "bit", nullable: false),
                    ExigePrazo = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_ChecklistModeloItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChecklistModeloItens_ChecklistModelos_ChecklistModeloId",
                        column: x => x.ChecklistModeloId,
                        principalTable: "ChecklistModelos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inspecoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoInspecao = table.Column<int>(type: "int", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AtividadeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChecklistModeloId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Data = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResponsavelUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_Inspecoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inspecoes_Atividades_AtividadeId",
                        column: x => x.AtividadeId,
                        principalTable: "Atividades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inspecoes_ChecklistModelos_ChecklistModeloId",
                        column: x => x.ChecklistModeloId,
                        principalTable: "ChecklistModelos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inspecoes_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Inspecoes_Usuarios_ResponsavelUsuarioId",
                        column: x => x.ResponsavelUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InspecaoItemRespostas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspecaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChecklistModeloItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatusItem = table.Column<int>(type: "int", nullable: true),
                    Observacao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ResponsavelUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Prazo = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_InspecaoItemRespostas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspecaoItemRespostas_ChecklistModeloItens_ChecklistModeloItemId",
                        column: x => x.ChecklistModeloItemId,
                        principalTable: "ChecklistModeloItens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspecaoItemRespostas_Inspecoes_InspecaoId",
                        column: x => x.InspecaoId,
                        principalTable: "Inspecoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InspecaoItemRespostas_Usuarios_ResponsavelUsuarioId",
                        column: x => x.ResponsavelUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistModeloItens_ChecklistModeloId",
                table: "ChecklistModeloItens",
                column: "ChecklistModeloId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistModelos_ChecklistModeloAnteriorId",
                table: "ChecklistModelos",
                column: "ChecklistModeloAnteriorId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistModelos_TipoInspecao",
                table: "ChecklistModelos",
                column: "TipoInspecao");

            migrationBuilder.CreateIndex(
                name: "IX_InspecaoItemRespostas_ChecklistModeloItemId",
                table: "InspecaoItemRespostas",
                column: "ChecklistModeloItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InspecaoItemRespostas_InspecaoId_ChecklistModeloItemId",
                table: "InspecaoItemRespostas",
                columns: new[] { "InspecaoId", "ChecklistModeloItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_InspecaoItemRespostas_ResponsavelUsuarioId",
                table: "InspecaoItemRespostas",
                column: "ResponsavelUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Inspecoes_AtividadeId",
                table: "Inspecoes",
                column: "AtividadeId");

            migrationBuilder.CreateIndex(
                name: "IX_Inspecoes_ChecklistModeloId",
                table: "Inspecoes",
                column: "ChecklistModeloId");

            migrationBuilder.CreateIndex(
                name: "IX_Inspecoes_ObraId",
                table: "Inspecoes",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_Inspecoes_ResponsavelUsuarioId",
                table: "Inspecoes",
                column: "ResponsavelUsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InspecaoItemRespostas");

            migrationBuilder.DropTable(
                name: "ChecklistModeloItens");

            migrationBuilder.DropTable(
                name: "Inspecoes");

            migrationBuilder.DropTable(
                name: "ChecklistModelos");
        }
    }
}
