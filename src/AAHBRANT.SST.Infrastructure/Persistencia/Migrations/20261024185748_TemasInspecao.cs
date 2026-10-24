using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class TemasInspecao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ItemInspecionavelId",
                table: "Inspecoes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TemaInspecaoId",
                table: "ChecklistModelos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TemasInspecao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
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
                    table.PrimaryKey("PK_TemasInspecao", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItensInspecionaveis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemaInspecaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Identificacao = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_ItensInspecionaveis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensInspecionaveis_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItensInspecionaveis_TemasInspecao_TemaInspecaoId",
                        column: x => x.TemaInspecaoId,
                        principalTable: "TemasInspecao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Inspecoes_ItemInspecionavelId",
                table: "Inspecoes",
                column: "ItemInspecionavelId",
                unique: true,
                filter: "[ItemInspecionavelId] IS NOT NULL AND [Status] = 1 AND [Ativo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistModelos_TemaInspecaoId",
                table: "ChecklistModelos",
                column: "TemaInspecaoId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensInspecionaveis_ObraId",
                table: "ItensInspecionaveis",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensInspecionaveis_TemaInspecaoId_ObraId",
                table: "ItensInspecionaveis",
                columns: new[] { "TemaInspecaoId", "ObraId" });

            migrationBuilder.CreateIndex(
                name: "IX_ItensInspecionaveis_TemaInspecaoId_ObraId_Identificacao",
                table: "ItensInspecionaveis",
                columns: new[] { "TemaInspecaoId", "ObraId", "Identificacao" },
                unique: true,
                filter: "[Ativo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TemasInspecao_Nome",
                table: "TemasInspecao",
                column: "Nome",
                unique: true,
                filter: "[Ativo] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_ChecklistModelos_TemasInspecao_TemaInspecaoId",
                table: "ChecklistModelos",
                column: "TemaInspecaoId",
                principalTable: "TemasInspecao",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Inspecoes_ItensInspecionaveis_ItemInspecionavelId",
                table: "Inspecoes",
                column: "ItemInspecionavelId",
                principalTable: "ItensInspecionaveis",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChecklistModelos_TemasInspecao_TemaInspecaoId",
                table: "ChecklistModelos");

            migrationBuilder.DropForeignKey(
                name: "FK_Inspecoes_ItensInspecionaveis_ItemInspecionavelId",
                table: "Inspecoes");

            migrationBuilder.DropTable(
                name: "ItensInspecionaveis");

            migrationBuilder.DropTable(
                name: "TemasInspecao");

            migrationBuilder.DropIndex(
                name: "IX_Inspecoes_ItemInspecionavelId",
                table: "Inspecoes");

            migrationBuilder.DropIndex(
                name: "IX_ChecklistModelos_TemaInspecaoId",
                table: "ChecklistModelos");

            migrationBuilder.DropColumn(
                name: "ItemInspecionavelId",
                table: "Inspecoes");

            migrationBuilder.DropColumn(
                name: "TemaInspecaoId",
                table: "ChecklistModelos");
        }
    }
}
