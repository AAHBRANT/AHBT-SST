using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CriarModuloAlojamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AlojamentoId",
                table: "Inspecoes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Alojamentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Endereco = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    GrhAlojamentoId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DataUltimaSincronizacao = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_Alojamentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Alojamentos_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracoesAlojamento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiasParaInspecaoAtrasada = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ConfiguracoesAlojamento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AlojamentoMoradores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlojamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataDesde = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataSaida = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_AlojamentoMoradores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlojamentoMoradores_Alojamentos_AlojamentoId",
                        column: x => x.AlojamentoId,
                        principalTable: "Alojamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AlojamentoMoradores_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Inspecoes_AlojamentoId",
                table: "Inspecoes",
                column: "AlojamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_AlojamentoMoradores_AlojamentoId",
                table: "AlojamentoMoradores",
                column: "AlojamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_AlojamentoMoradores_TrabalhadorId",
                table: "AlojamentoMoradores",
                column: "TrabalhadorId",
                unique: true,
                filter: "[DataSaida] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Alojamentos_GrhAlojamentoId",
                table: "Alojamentos",
                column: "GrhAlojamentoId",
                unique: true,
                filter: "[GrhAlojamentoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Alojamentos_ObraId",
                table: "Alojamentos",
                column: "ObraId");

            migrationBuilder.AddForeignKey(
                name: "FK_Inspecoes_Alojamentos_AlojamentoId",
                table: "Inspecoes",
                column: "AlojamentoId",
                principalTable: "Alojamentos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Inspecoes_Alojamentos_AlojamentoId",
                table: "Inspecoes");

            migrationBuilder.DropTable(
                name: "AlojamentoMoradores");

            migrationBuilder.DropTable(
                name: "ConfiguracoesAlojamento");

            migrationBuilder.DropTable(
                name: "Alojamentos");

            migrationBuilder.DropIndex(
                name: "IX_Inspecoes_AlojamentoId",
                table: "Inspecoes");

            migrationBuilder.DropColumn(
                name: "AlojamentoId",
                table: "Inspecoes");
        }
    }
}
