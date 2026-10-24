using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CriarReunioesAnaliseOcorrencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReunioesAnaliseOcorrencia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcidenteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcaoPlanoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Inicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Fim = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OrganizadorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Participantes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Situacao = table.Column<int>(type: "int", nullable: false),
                    GraphEventId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LinkTeams = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MotivoFalha = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_ReunioesAnaliseOcorrencia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReunioesAnaliseOcorrencia_Acidentes_AcidenteId",
                        column: x => x.AcidenteId,
                        principalTable: "Acidentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReunioesAnaliseOcorrencia_AcidenteId",
                table: "ReunioesAnaliseOcorrencia",
                column: "AcidenteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReunioesAnaliseOcorrencia");
        }
    }
}
