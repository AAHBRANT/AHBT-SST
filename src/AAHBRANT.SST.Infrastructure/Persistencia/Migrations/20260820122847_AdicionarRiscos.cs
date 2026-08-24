using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarRiscos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Atividades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Atividades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Atividades_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MatrizRiscoConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NumNiveisProbabilidade = table.Column<int>(type: "int", nullable: false),
                    NumNiveisSeveridade = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_MatrizRiscoConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatrizRiscoConfigs_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Perigos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Agente = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Fonte = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Perigos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatrizRiscoCelulas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatrizRiscoConfigId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Probabilidade = table.Column<int>(type: "int", nullable: false),
                    Severidade = table.Column<int>(type: "int", nullable: false),
                    NivelRisco = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_MatrizRiscoCelulas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatrizRiscoCelulas_MatrizRiscoConfigs_MatrizRiscoConfigId",
                        column: x => x.MatrizRiscoConfigId,
                        principalTable: "MatrizRiscoConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Riscos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AtividadeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerigoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ambiente = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Exposicao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Consequencia = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Probabilidade = table.Column<int>(type: "int", nullable: false),
                    Severidade = table.Column<int>(type: "int", nullable: false),
                    NivelRisco = table.Column<int>(type: "int", nullable: false),
                    ControlesExistentes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ControlesAdicionais = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponsavelUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Prazo = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_Riscos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Riscos_Atividades_AtividadeId",
                        column: x => x.AtividadeId,
                        principalTable: "Atividades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Riscos_Perigos_PerigoId",
                        column: x => x.PerigoId,
                        principalTable: "Perigos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Riscos_Usuarios_ResponsavelUsuarioId",
                        column: x => x.ResponsavelUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiscoTrabalhadorExpostos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiscoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_RiscoTrabalhadorExpostos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiscoTrabalhadorExpostos_Riscos_RiscoId",
                        column: x => x.RiscoId,
                        principalTable: "Riscos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RiscoTrabalhadorExpostos_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Atividades_ObraId",
                table: "Atividades",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_MatrizRiscoCelulas_MatrizRiscoConfigId_Probabilidade_Severidade",
                table: "MatrizRiscoCelulas",
                columns: new[] { "MatrizRiscoConfigId", "Probabilidade", "Severidade" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatrizRiscoConfigs_EmpresaId",
                table: "MatrizRiscoConfigs",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Riscos_AtividadeId_NivelRisco",
                table: "Riscos",
                columns: new[] { "AtividadeId", "NivelRisco" });

            migrationBuilder.CreateIndex(
                name: "IX_Riscos_PerigoId",
                table: "Riscos",
                column: "PerigoId");

            migrationBuilder.CreateIndex(
                name: "IX_Riscos_ResponsavelUsuarioId",
                table: "Riscos",
                column: "ResponsavelUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_RiscoTrabalhadorExpostos_RiscoId_TrabalhadorId",
                table: "RiscoTrabalhadorExpostos",
                columns: new[] { "RiscoId", "TrabalhadorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RiscoTrabalhadorExpostos_TrabalhadorId",
                table: "RiscoTrabalhadorExpostos",
                column: "TrabalhadorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatrizRiscoCelulas");

            migrationBuilder.DropTable(
                name: "RiscoTrabalhadorExpostos");

            migrationBuilder.DropTable(
                name: "MatrizRiscoConfigs");

            migrationBuilder.DropTable(
                name: "Riscos");

            migrationBuilder.DropTable(
                name: "Atividades");

            migrationBuilder.DropTable(
                name: "Perigos");
        }
    }
}
