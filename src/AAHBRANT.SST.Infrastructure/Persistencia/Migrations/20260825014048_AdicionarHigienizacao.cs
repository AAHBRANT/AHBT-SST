using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarHigienizacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ItensHigienizacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Local = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PeriodicidadeDias = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ItensHigienizacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensHigienizacao_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegistrosHigienizacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemHigienizacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Observacoes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FotoConteudo = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    FotoContentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_RegistrosHigienizacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrosHigienizacao_ItensHigienizacao_ItemHigienizacaoId",
                        column: x => x.ItemHigienizacaoId,
                        principalTable: "ItensHigienizacao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RegistrosHigienizacao_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItensHigienizacao_ObraId",
                table: "ItensHigienizacao",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosHigienizacao_ItemHigienizacaoId",
                table: "RegistrosHigienizacao",
                column: "ItemHigienizacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosHigienizacao_TrabalhadorId",
                table: "RegistrosHigienizacao",
                column: "TrabalhadorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegistrosHigienizacao");

            migrationBuilder.DropTable(
                name: "ItensHigienizacao");
        }
    }
}
