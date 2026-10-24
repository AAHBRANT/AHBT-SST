using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CriarTemasDdsAgendados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TemasDdsAgendados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Data = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CatalogoTemaDdsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrigemTipo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OrigemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DescricaoOrigem = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AcaoPlanoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DdsId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AplicadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_TemasDdsAgendados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemasDdsAgendados_CatalogosTemaDds_CatalogoTemaDdsId",
                        column: x => x.CatalogoTemaDdsId,
                        principalTable: "CatalogosTemaDds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TemasDdsAgendados_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TemasDdsAgendados_CatalogoTemaDdsId",
                table: "TemasDdsAgendados",
                column: "CatalogoTemaDdsId");

            migrationBuilder.CreateIndex(
                name: "IX_TemasDdsAgendados_ObraId_Data",
                table: "TemasDdsAgendados",
                columns: new[] { "ObraId", "Data" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TemasDdsAgendados");
        }
    }
}
