using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarGeracaoPgrEap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GeracoesPgrEap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DataElaboracao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataProximaRevisao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ElaboradoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ElaboradoPorNome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NomeArquivoEap = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    OrigemEap = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Passo = table.Column<int>(type: "int", nullable: false),
                    EstruturaJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MotivoDevolucao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AprovadoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AprovadoPorNome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AprovadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PgrId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_GeracoesPgrEap", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeracoesPgrEap_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GeracoesPgrEap_Pgrs_PgrId",
                        column: x => x.PgrId,
                        principalTable: "Pgrs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GeracoesPgrEap_ObraId_Status",
                table: "GeracoesPgrEap",
                columns: new[] { "ObraId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GeracoesPgrEap_PgrId",
                table: "GeracoesPgrEap",
                column: "PgrId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GeracoesPgrEap");
        }
    }
}
