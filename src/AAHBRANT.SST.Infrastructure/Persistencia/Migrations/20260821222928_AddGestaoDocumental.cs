using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AddGestaoDocumental : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentosGestao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Categoria = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OrigemDocumento = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResponsavelUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Versao = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Validade = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataEmissao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataRevisao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequisitoLegalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SetorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Arquivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_DocumentosGestao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentosGestao_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentosGestao_RequisitosLegais_RequisitoLegalId",
                        column: x => x.RequisitoLegalId,
                        principalTable: "RequisitosLegais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentosGestao_Setores_SetorId",
                        column: x => x.SetorId,
                        principalTable: "Setores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentosGestao_Usuarios_ResponsavelUsuarioId",
                        column: x => x.ResponsavelUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentoRevisoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroRevisao = table.Column<int>(type: "int", nullable: false),
                    DataRevisao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ResponsavelUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_DocumentoRevisoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentoRevisoes_DocumentosGestao_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "DocumentosGestao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentoRevisoes_Usuarios_ResponsavelUsuarioId",
                        column: x => x.ResponsavelUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoRevisoes_DocumentoId",
                table: "DocumentoRevisoes",
                column: "DocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoRevisoes_ResponsavelUsuarioId",
                table: "DocumentoRevisoes",
                column: "ResponsavelUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosGestao_Categoria",
                table: "DocumentosGestao",
                column: "Categoria");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosGestao_ObraId",
                table: "DocumentosGestao",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosGestao_RequisitoLegalId",
                table: "DocumentosGestao",
                column: "RequisitoLegalId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosGestao_ResponsavelUsuarioId",
                table: "DocumentosGestao",
                column: "ResponsavelUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosGestao_SetorId",
                table: "DocumentosGestao",
                column: "SetorId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosGestao_Status",
                table: "DocumentosGestao",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosGestao_Tipo",
                table: "DocumentosGestao",
                column: "Tipo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentoRevisoes");

            migrationBuilder.DropTable(
                name: "DocumentosGestao");
        }
    }
}
