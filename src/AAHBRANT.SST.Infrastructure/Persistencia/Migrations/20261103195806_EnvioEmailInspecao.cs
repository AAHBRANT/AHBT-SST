using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class EnvioEmailInspecao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContatosEmailRelatorio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_ContatosEmailRelatorio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContatosEmailRelatorio_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContatosEmailRelatorio_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EnviosEmailInspecao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspecaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnviadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EnviadoPorNome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Destinatarios = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Falhas = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Mensagem = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_EnviosEmailInspecao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnviosEmailInspecao_Inspecoes_InspecaoId",
                        column: x => x.InspecaoId,
                        principalTable: "Inspecoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContatosEmailRelatorio_ObraId_Email",
                table: "ContatosEmailRelatorio",
                columns: new[] { "ObraId", "Email" },
                unique: true,
                filter: "[Ativo] = 1 AND [ObraId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContatosEmailRelatorio_UsuarioId_Email",
                table: "ContatosEmailRelatorio",
                columns: new[] { "UsuarioId", "Email" },
                unique: true,
                filter: "[Ativo] = 1 AND [UsuarioId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EnviosEmailInspecao_InspecaoId",
                table: "EnviosEmailInspecao",
                column: "InspecaoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContatosEmailRelatorio");

            migrationBuilder.DropTable(
                name: "EnviosEmailInspecao");
        }
    }
}
