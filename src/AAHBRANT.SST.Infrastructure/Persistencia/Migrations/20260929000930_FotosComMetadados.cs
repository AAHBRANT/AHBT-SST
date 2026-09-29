using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class FotosComMetadados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FotoDepoisMetadadosJson",
                table: "InspecaoItemRespostas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FotoMetadadosJson",
                table: "InspecaoItemRespostas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FotoMetadadosJson",
                table: "FotosEvidenciaSessaoTreinamento",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FotoMetadadosJson",
                table: "DdsFotosEvidencia",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AcidentesFotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcidenteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    FotoConteudo = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    FotoContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FotoMetadadosJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_AcidentesFotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcidentesFotos_Acidentes_AcidenteId",
                        column: x => x.AcidenteId,
                        principalTable: "Acidentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcidentesFotos_AcidenteId_Ordem",
                table: "AcidentesFotos",
                columns: new[] { "AcidenteId", "Ordem" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcidentesFotos");

            migrationBuilder.DropColumn(
                name: "FotoDepoisMetadadosJson",
                table: "InspecaoItemRespostas");

            migrationBuilder.DropColumn(
                name: "FotoMetadadosJson",
                table: "InspecaoItemRespostas");

            migrationBuilder.DropColumn(
                name: "FotoMetadadosJson",
                table: "FotosEvidenciaSessaoTreinamento");

            migrationBuilder.DropColumn(
                name: "FotoMetadadosJson",
                table: "DdsFotosEvidencia");
        }
    }
}
