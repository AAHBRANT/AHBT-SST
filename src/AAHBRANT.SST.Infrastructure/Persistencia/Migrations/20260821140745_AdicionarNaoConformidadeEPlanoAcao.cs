using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarNaoConformidadeEPlanoAcao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AcoesPlano",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrigemTipo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OrigemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ResponsavelUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Prioridade = table.Column<int>(type: "int", nullable: false),
                    Prazo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DataConclusao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataValidacao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValidadoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AcoesPlano", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcoesPlano_Usuarios_ResponsavelUsuarioId",
                        column: x => x.ResponsavelUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcoesPlano_Usuarios_ValidadoPorUsuarioId",
                        column: x => x.ValidadoPorUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NaoConformidades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrigemDeteccao = table.Column<int>(type: "int", nullable: false),
                    RequisitoRelacionado = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Local = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AtividadeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RiscoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResponsavelUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Prazo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DataConclusao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ObservacoesEncerramento = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_NaoConformidades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NaoConformidades_Atividades_AtividadeId",
                        column: x => x.AtividadeId,
                        principalTable: "Atividades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NaoConformidades_Riscos_RiscoId",
                        column: x => x.RiscoId,
                        principalTable: "Riscos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NaoConformidades_Usuarios_ResponsavelUsuarioId",
                        column: x => x.ResponsavelUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcoesPlano_OrigemTipo_OrigemId",
                table: "AcoesPlano",
                columns: new[] { "OrigemTipo", "OrigemId" });

            migrationBuilder.CreateIndex(
                name: "IX_AcoesPlano_ResponsavelUsuarioId",
                table: "AcoesPlano",
                column: "ResponsavelUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_AcoesPlano_ValidadoPorUsuarioId",
                table: "AcoesPlano",
                column: "ValidadoPorUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_NaoConformidades_AtividadeId",
                table: "NaoConformidades",
                column: "AtividadeId");

            migrationBuilder.CreateIndex(
                name: "IX_NaoConformidades_ResponsavelUsuarioId",
                table: "NaoConformidades",
                column: "ResponsavelUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_NaoConformidades_RiscoId",
                table: "NaoConformidades",
                column: "RiscoId");

            migrationBuilder.CreateIndex(
                name: "IX_NaoConformidades_Status",
                table: "NaoConformidades",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcoesPlano");

            migrationBuilder.DropTable(
                name: "NaoConformidades");
        }
    }
}
