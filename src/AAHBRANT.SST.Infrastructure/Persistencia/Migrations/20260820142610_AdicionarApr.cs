using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarApr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Aprs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AtividadeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Local = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EquipeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Data = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Validade = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AprovadoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAprovacao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoReprovacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_Aprs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Aprs_Atividades_AtividadeId",
                        column: x => x.AtividadeId,
                        principalTable: "Atividades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Aprs_Equipes_EquipeId",
                        column: x => x.EquipeId,
                        principalTable: "Equipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Aprs_Usuarios_AprovadoPorUsuarioId",
                        column: x => x.AprovadoPorUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AprAssinaturas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AprId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Papel = table.Column<int>(type: "int", nullable: false),
                    DataAssinatura = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_AprAssinaturas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AprAssinaturas_Aprs_AprId",
                        column: x => x.AprId,
                        principalTable: "Aprs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AprAssinaturas_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AprEtapas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AprId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MedidasPreventivas = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_AprEtapas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AprEtapas_Aprs_AprId",
                        column: x => x.AprId,
                        principalTable: "Aprs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AprResponsaveis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AprId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_AprResponsaveis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AprResponsaveis_Aprs_AprId",
                        column: x => x.AprId,
                        principalTable: "Aprs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AprResponsaveis_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AprEtapaRiscos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AprEtapaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiscoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_AprEtapaRiscos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AprEtapaRiscos_AprEtapas_AprEtapaId",
                        column: x => x.AprEtapaId,
                        principalTable: "AprEtapas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AprEtapaRiscos_Riscos_RiscoId",
                        column: x => x.RiscoId,
                        principalTable: "Riscos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AprAssinaturas_AprId_TrabalhadorId_Papel",
                table: "AprAssinaturas",
                columns: new[] { "AprId", "TrabalhadorId", "Papel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AprAssinaturas_TrabalhadorId",
                table: "AprAssinaturas",
                column: "TrabalhadorId");

            migrationBuilder.CreateIndex(
                name: "IX_AprEtapaRiscos_AprEtapaId_RiscoId",
                table: "AprEtapaRiscos",
                columns: new[] { "AprEtapaId", "RiscoId" });

            migrationBuilder.CreateIndex(
                name: "IX_AprEtapaRiscos_RiscoId",
                table: "AprEtapaRiscos",
                column: "RiscoId");

            migrationBuilder.CreateIndex(
                name: "IX_AprEtapas_AprId_Ordem",
                table: "AprEtapas",
                columns: new[] { "AprId", "Ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_AprResponsaveis_AprId_TrabalhadorId",
                table: "AprResponsaveis",
                columns: new[] { "AprId", "TrabalhadorId" });

            migrationBuilder.CreateIndex(
                name: "IX_AprResponsaveis_TrabalhadorId",
                table: "AprResponsaveis",
                column: "TrabalhadorId");

            migrationBuilder.CreateIndex(
                name: "IX_Aprs_AprovadoPorUsuarioId",
                table: "Aprs",
                column: "AprovadoPorUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Aprs_AtividadeId",
                table: "Aprs",
                column: "AtividadeId");

            migrationBuilder.CreateIndex(
                name: "IX_Aprs_EquipeId",
                table: "Aprs",
                column: "EquipeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AprAssinaturas");

            migrationBuilder.DropTable(
                name: "AprEtapaRiscos");

            migrationBuilder.DropTable(
                name: "AprResponsaveis");

            migrationBuilder.DropTable(
                name: "AprEtapas");

            migrationBuilder.DropTable(
                name: "Aprs");
        }
    }
}
