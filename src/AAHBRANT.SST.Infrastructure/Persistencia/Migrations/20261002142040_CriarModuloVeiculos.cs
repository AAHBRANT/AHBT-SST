using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CriarModuloVeiculos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VeiculoId",
                table: "Inspecoes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TipoVeiculo",
                table: "ChecklistModelos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Veiculos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    PlacaPrefixo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    MarcaModelo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Ano = table.Column<int>(type: "int", nullable: true),
                    Cor = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Empresa = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Responsavel = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
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
                    table.PrimaryKey("PK_Veiculos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Veiculos_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Inspecoes_VeiculoId",
                table: "Inspecoes",
                column: "VeiculoId",
                unique: true,
                filter: "[VeiculoId] IS NOT NULL AND [Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistModelos_TipoVeiculo",
                table: "ChecklistModelos",
                column: "TipoVeiculo");

            migrationBuilder.CreateIndex(
                name: "IX_Veiculos_ObraId_Tipo",
                table: "Veiculos",
                columns: new[] { "ObraId", "Tipo" });

            migrationBuilder.AddForeignKey(
                name: "FK_Inspecoes_Veiculos_VeiculoId",
                table: "Inspecoes",
                column: "VeiculoId",
                principalTable: "Veiculos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Inspecoes_Veiculos_VeiculoId",
                table: "Inspecoes");

            migrationBuilder.DropTable(
                name: "Veiculos");

            migrationBuilder.DropIndex(
                name: "IX_Inspecoes_VeiculoId",
                table: "Inspecoes");

            migrationBuilder.DropIndex(
                name: "IX_ChecklistModelos_TipoVeiculo",
                table: "ChecklistModelos");

            migrationBuilder.DropColumn(
                name: "VeiculoId",
                table: "Inspecoes");

            migrationBuilder.DropColumn(
                name: "TipoVeiculo",
                table: "ChecklistModelos");
        }
    }
}
