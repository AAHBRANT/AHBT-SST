using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CatalogoEpiMatrizPorObra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MatrizEpiFuncoes_FuncaoId_CatalogoEpiId",
                table: "MatrizEpiFuncoes");

            migrationBuilder.AddColumn<Guid>(
                name: "ObraId",
                table: "MatrizEpiFuncoes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ObraId",
                table: "CatalogoEpis",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatrizEpiFuncoes_FuncaoId_CatalogoEpiId_ObraId",
                table: "MatrizEpiFuncoes",
                columns: new[] { "FuncaoId", "CatalogoEpiId", "ObraId" },
                unique: true,
                filter: "[ObraId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MatrizEpiFuncoes_FuncaoId_CatalogoEpiId_SemObra",
                table: "MatrizEpiFuncoes",
                columns: new[] { "FuncaoId", "CatalogoEpiId" },
                unique: true,
                filter: "[ObraId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MatrizEpiFuncoes_ObraId",
                table: "MatrizEpiFuncoes",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogoEpis_ObraId",
                table: "CatalogoEpis",
                column: "ObraId");

            migrationBuilder.AddForeignKey(
                name: "FK_CatalogoEpis_Obras_ObraId",
                table: "CatalogoEpis",
                column: "ObraId",
                principalTable: "Obras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MatrizEpiFuncoes_Obras_ObraId",
                table: "MatrizEpiFuncoes",
                column: "ObraId",
                principalTable: "Obras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CatalogoEpis_Obras_ObraId",
                table: "CatalogoEpis");

            migrationBuilder.DropForeignKey(
                name: "FK_MatrizEpiFuncoes_Obras_ObraId",
                table: "MatrizEpiFuncoes");

            migrationBuilder.DropIndex(
                name: "IX_MatrizEpiFuncoes_FuncaoId_CatalogoEpiId_ObraId",
                table: "MatrizEpiFuncoes");

            migrationBuilder.DropIndex(
                name: "IX_MatrizEpiFuncoes_FuncaoId_CatalogoEpiId_SemObra",
                table: "MatrizEpiFuncoes");

            migrationBuilder.DropIndex(
                name: "IX_MatrizEpiFuncoes_ObraId",
                table: "MatrizEpiFuncoes");

            migrationBuilder.DropIndex(
                name: "IX_CatalogoEpis_ObraId",
                table: "CatalogoEpis");

            migrationBuilder.DropColumn(
                name: "ObraId",
                table: "MatrizEpiFuncoes");

            migrationBuilder.DropColumn(
                name: "ObraId",
                table: "CatalogoEpis");

            migrationBuilder.CreateIndex(
                name: "IX_MatrizEpiFuncoes_FuncaoId_CatalogoEpiId",
                table: "MatrizEpiFuncoes",
                columns: new[] { "FuncaoId", "CatalogoEpiId" },
                unique: true);
        }
    }
}
