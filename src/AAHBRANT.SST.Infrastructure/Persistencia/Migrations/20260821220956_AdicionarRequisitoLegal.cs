using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarRequisitoLegal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RequisitosLegais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Norma = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Item = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Tema = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Requisito = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Aplicabilidade = table.Column<bool>(type: "bit", nullable: false),
                    Justificativa = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Evidencia = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResponsavelUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Periodicidade = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Prazo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UltimaRevisao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProximaRevisao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_RequisitosLegais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequisitosLegais_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequisitosLegais_Usuarios_ResponsavelUsuarioId",
                        column: x => x.ResponsavelUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RequisitosLegais_Norma",
                table: "RequisitosLegais",
                column: "Norma");

            migrationBuilder.CreateIndex(
                name: "IX_RequisitosLegais_ObraId",
                table: "RequisitosLegais",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_RequisitosLegais_ResponsavelUsuarioId",
                table: "RequisitosLegais",
                column: "ResponsavelUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_RequisitosLegais_Status",
                table: "RequisitosLegais",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequisitosLegais");
        }
    }
}
