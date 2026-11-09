using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CriarModuloCameras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cameras",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AreaSstId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Habilitada = table.Column<bool>(type: "bit", nullable: false),
                    IntervaloMinimoAlertasMinutos = table.Column<int>(type: "int", nullable: false),
                    UltimaAnaliseEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimoAlertaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_Cameras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cameras_AreasSst_AreaSstId",
                        column: x => x.AreaSstId,
                        principalTable: "AreasSst",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cameras_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OcorrenciasEpiCamera",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CameraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapturadaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecebidaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Foto = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    PessoasNaCena = table.Column<int>(type: "int", nullable: false),
                    SemCapacete = table.Column<int>(type: "int", nullable: false),
                    SemColete = table.Column<int>(type: "int", nullable: false),
                    Confianca = table.Column<int>(type: "int", nullable: false),
                    AlertaEnviado = table.Column<bool>(type: "bit", nullable: false),
                    AlertaEnviadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VinculadoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VinculadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Situacao = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_OcorrenciasEpiCamera", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OcorrenciasEpiCamera_Cameras_CameraId",
                        column: x => x.CameraId,
                        principalTable: "Cameras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OcorrenciasEpiCamera_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cameras_AreaSstId",
                table: "Cameras",
                column: "AreaSstId");

            migrationBuilder.CreateIndex(
                name: "IX_Cameras_ObraId_Nome",
                table: "Cameras",
                columns: new[] { "ObraId", "Nome" });

            migrationBuilder.CreateIndex(
                name: "IX_OcorrenciasEpiCamera_CameraId_RecebidaEmUtc",
                table: "OcorrenciasEpiCamera",
                columns: new[] { "CameraId", "RecebidaEmUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OcorrenciasEpiCamera_ObraId_RecebidaEmUtc",
                table: "OcorrenciasEpiCamera",
                columns: new[] { "ObraId", "RecebidaEmUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OcorrenciasEpiCamera_TrabalhadorId",
                table: "OcorrenciasEpiCamera",
                column: "TrabalhadorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OcorrenciasEpiCamera");

            migrationBuilder.DropTable(
                name: "Cameras");
        }
    }
}
