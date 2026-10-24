using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarSelecaoFuncionariosDds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DdsFuncionariosSelecionados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DdsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_DdsFuncionariosSelecionados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DdsFuncionariosSelecionados_Dds_DdsId",
                        column: x => x.DdsId,
                        principalTable: "Dds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DdsFuncionariosSelecionados_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DdsFuncionariosSelecionados_DdsId_TrabalhadorId",
                table: "DdsFuncionariosSelecionados",
                columns: new[] { "DdsId", "TrabalhadorId" },
                unique: true,
                filter: "[Ativo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_DdsFuncionariosSelecionados_TrabalhadorId",
                table: "DdsFuncionariosSelecionados",
                column: "TrabalhadorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DdsFuncionariosSelecionados");
        }
    }
}
