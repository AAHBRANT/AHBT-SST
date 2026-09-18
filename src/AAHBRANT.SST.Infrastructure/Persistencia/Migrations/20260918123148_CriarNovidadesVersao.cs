using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CriarNovidadesVersao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NovidadesVersao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Versao = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DataPublicacao = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_NovidadesVersao", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NovidadesVersaoItens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NovidadeVersaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Antes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Agora = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Ordem = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_NovidadesVersaoItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NovidadesVersaoItens_NovidadesVersao_NovidadeVersaoId",
                        column: x => x.NovidadeVersaoId,
                        principalTable: "NovidadesVersao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NovidadesVisualizacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NovidadeVersaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VistoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_NovidadesVisualizacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NovidadesVisualizacao_NovidadesVersao_NovidadeVersaoId",
                        column: x => x.NovidadeVersaoId,
                        principalTable: "NovidadesVersao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NovidadesVisualizacao_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NovidadesVersao_DataPublicacao",
                table: "NovidadesVersao",
                column: "DataPublicacao");

            migrationBuilder.CreateIndex(
                name: "IX_NovidadesVersaoItens_NovidadeVersaoId",
                table: "NovidadesVersaoItens",
                column: "NovidadeVersaoId");

            migrationBuilder.CreateIndex(
                name: "IX_NovidadesVisualizacao_NovidadeVersaoId_UsuarioId",
                table: "NovidadesVisualizacao",
                columns: new[] { "NovidadeVersaoId", "UsuarioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NovidadesVisualizacao_UsuarioId",
                table: "NovidadesVisualizacao",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NovidadesVersaoItens");

            migrationBuilder.DropTable(
                name: "NovidadesVisualizacao");

            migrationBuilder.DropTable(
                name: "NovidadesVersao");
        }
    }
}
