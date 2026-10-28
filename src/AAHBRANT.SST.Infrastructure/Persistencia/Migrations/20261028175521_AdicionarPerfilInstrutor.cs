using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarPerfilInstrutor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InstrutorTrabalhadorId",
                table: "SessoesTreinamento",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PerfisInstrutor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Formacao = table.Column<int>(type: "int", nullable: false),
                    Conselho = table.Column<int>(type: "int", nullable: true),
                    NumeroRegistro = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    UfRegistro = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    PodeAssinarEmOutrasObras = table.Column<bool>(type: "bit", nullable: false),
                    AutorizadoOutrasObrasEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AutorizadoOutrasObrasPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_PerfisInstrutor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerfisInstrutor_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessoesTreinamento_InstrutorTrabalhadorId",
                table: "SessoesTreinamento",
                column: "InstrutorTrabalhadorId");

            migrationBuilder.CreateIndex(
                name: "IX_PerfisInstrutor_TrabalhadorId",
                table: "PerfisInstrutor",
                column: "TrabalhadorId",
                unique: true,
                filter: "[Ativo] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_SessoesTreinamento_Trabalhadores_InstrutorTrabalhadorId",
                table: "SessoesTreinamento",
                column: "InstrutorTrabalhadorId",
                principalTable: "Trabalhadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SessoesTreinamento_Trabalhadores_InstrutorTrabalhadorId",
                table: "SessoesTreinamento");

            migrationBuilder.DropTable(
                name: "PerfisInstrutor");

            migrationBuilder.DropIndex(
                name: "IX_SessoesTreinamento_InstrutorTrabalhadorId",
                table: "SessoesTreinamento");

            migrationBuilder.DropColumn(
                name: "InstrutorTrabalhadorId",
                table: "SessoesTreinamento");
        }
    }
}
