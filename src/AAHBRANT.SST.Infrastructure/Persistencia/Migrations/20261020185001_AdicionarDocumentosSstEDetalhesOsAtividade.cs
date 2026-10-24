using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarDocumentosSstEDetalhesOsAtividade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Codigo",
                table: "MateriaisApoio",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FormaConsulta",
                table: "MateriaisApoio",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncluirEmTodasOs",
                table: "MateriaisApoio",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Revisao",
                table: "MateriaisApoio",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Situacao",
                table: "MateriaisApoio",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Tipo",
                table: "MateriaisApoio",
                type: "int",
                nullable: false,
                defaultValue: 1); // material de apoio já existente

            migrationBuilder.AddColumn<string>(
                name: "CondicoesExecucao",
                table: "Atividades",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ferramentas",
                table: "Atividades",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AtividadeDocumentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AtividadeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaterialApoioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_AtividadeDocumentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AtividadeDocumentos_Atividades_AtividadeId",
                        column: x => x.AtividadeId,
                        principalTable: "Atividades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AtividadeDocumentos_MateriaisApoio_MaterialApoioId",
                        column: x => x.MaterialApoioId,
                        principalTable: "MateriaisApoio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AtividadeEpcs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AtividadeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogoEpcId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_AtividadeEpcs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AtividadeEpcs_Atividades_AtividadeId",
                        column: x => x.AtividadeId,
                        principalTable: "Atividades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AtividadeEpcs_CatalogoEpcs_CatalogoEpcId",
                        column: x => x.CatalogoEpcId,
                        principalTable: "CatalogoEpcs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AtividadeItensOs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AtividadeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Requisito = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    OrigemItem = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ValidadoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ValidadoPorNome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ValidadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_AtividadeItensOs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AtividadeItensOs_Atividades_AtividadeId",
                        column: x => x.AtividadeId,
                        principalTable: "Atividades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MateriaisApoio_Tipo",
                table: "MateriaisApoio",
                column: "Tipo");

            migrationBuilder.CreateIndex(
                name: "IX_AtividadeDocumentos_AtividadeId_MaterialApoioId",
                table: "AtividadeDocumentos",
                columns: new[] { "AtividadeId", "MaterialApoioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AtividadeDocumentos_MaterialApoioId",
                table: "AtividadeDocumentos",
                column: "MaterialApoioId");

            migrationBuilder.CreateIndex(
                name: "IX_AtividadeEpcs_AtividadeId_CatalogoEpcId",
                table: "AtividadeEpcs",
                columns: new[] { "AtividadeId", "CatalogoEpcId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AtividadeEpcs_CatalogoEpcId",
                table: "AtividadeEpcs",
                column: "CatalogoEpcId");

            migrationBuilder.CreateIndex(
                name: "IX_AtividadeItensOs_AtividadeId_Tipo",
                table: "AtividadeItensOs",
                columns: new[] { "AtividadeId", "Tipo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AtividadeDocumentos");

            migrationBuilder.DropTable(
                name: "AtividadeEpcs");

            migrationBuilder.DropTable(
                name: "AtividadeItensOs");

            migrationBuilder.DropIndex(
                name: "IX_MateriaisApoio_Tipo",
                table: "MateriaisApoio");

            migrationBuilder.DropColumn(
                name: "Codigo",
                table: "MateriaisApoio");

            migrationBuilder.DropColumn(
                name: "FormaConsulta",
                table: "MateriaisApoio");

            migrationBuilder.DropColumn(
                name: "IncluirEmTodasOs",
                table: "MateriaisApoio");

            migrationBuilder.DropColumn(
                name: "Revisao",
                table: "MateriaisApoio");

            migrationBuilder.DropColumn(
                name: "Situacao",
                table: "MateriaisApoio");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "MateriaisApoio");

            migrationBuilder.DropColumn(
                name: "CondicoesExecucao",
                table: "Atividades");

            migrationBuilder.DropColumn(
                name: "Ferramentas",
                table: "Atividades");
        }
    }
}
