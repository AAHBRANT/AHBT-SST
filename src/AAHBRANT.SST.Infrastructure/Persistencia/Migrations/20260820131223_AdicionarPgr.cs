using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarPgr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pgrs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DataElaboracao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataProximaRevisao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponsavelUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_Pgrs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pgrs_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pgrs_Usuarios_ResponsavelUsuarioId",
                        column: x => x.ResponsavelUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PgrRevisoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PgrId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroRevisao = table.Column<int>(type: "int", nullable: false),
                    DataRevisao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ResponsavelUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_PgrRevisoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PgrRevisoes_Pgrs_PgrId",
                        column: x => x.PgrId,
                        principalTable: "Pgrs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PgrRevisoes_Usuarios_ResponsavelUsuarioId",
                        column: x => x.ResponsavelUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlanoAcaoItens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PgrId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiscoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ResponsavelUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Prazo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataConclusao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_PlanoAcaoItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanoAcaoItens_Pgrs_PgrId",
                        column: x => x.PgrId,
                        principalTable: "Pgrs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanoAcaoItens_Riscos_RiscoId",
                        column: x => x.RiscoId,
                        principalTable: "Riscos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlanoAcaoItens_Usuarios_ResponsavelUsuarioId",
                        column: x => x.ResponsavelUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PgrRevisoes_PgrId_NumeroRevisao",
                table: "PgrRevisoes",
                columns: new[] { "PgrId", "NumeroRevisao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PgrRevisoes_ResponsavelUsuarioId",
                table: "PgrRevisoes",
                column: "ResponsavelUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Pgrs_ObraId",
                table: "Pgrs",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_Pgrs_ResponsavelUsuarioId",
                table: "Pgrs",
                column: "ResponsavelUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanoAcaoItens_PgrId_Status",
                table: "PlanoAcaoItens",
                columns: new[] { "PgrId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanoAcaoItens_ResponsavelUsuarioId",
                table: "PlanoAcaoItens",
                column: "ResponsavelUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanoAcaoItens_RiscoId",
                table: "PlanoAcaoItens",
                column: "RiscoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PgrRevisoes");

            migrationBuilder.DropTable(
                name: "PlanoAcaoItens");

            migrationBuilder.DropTable(
                name: "Pgrs");
        }
    }
}
