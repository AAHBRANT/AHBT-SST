using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class DdsSemanal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogosTemaDds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_CatalogosTemaDds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DdsSemanais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    EmpresaTerceirizada = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NumeroDocumento = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LocalFrenteServico = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResponsavelUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataInicioSemana = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataFimSemana = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ResponsavelObraSstUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResponsavelEmpresaTerceirizadaNome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResponsavelEmpresaTerceirizadaFuncao = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    EncerradaEm = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_DdsSemanais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DdsSemanais_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DdsSemanais_Usuarios_ResponsavelUsuarioId",
                        column: x => x.ResponsavelUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DdsSemanais_Usuarios_ResponsavelObraSstUsuarioId",
                        column: x => x.ResponsavelObraSstUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddColumn<Guid>(
                name: "DdsSemanalId",
                table: "Dds",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrigemTema",
                table: "Dds",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<Guid>(
                name: "CatalogoTemaDdsId",
                table: "Dds",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Ordem",
                table: "DdsAtividades",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DdsFotosEvidencia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DdsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    FotoConteudo = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    FotoContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_DdsFotosEvidencia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DdsFotosEvidencia_Dds_DdsId",
                        column: x => x.DdsId,
                        principalTable: "Dds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Dds_DdsSemanalId",
                table: "Dds",
                column: "DdsSemanalId");

            migrationBuilder.CreateIndex(
                name: "IX_Dds_CatalogoTemaDdsId",
                table: "Dds",
                column: "CatalogoTemaDdsId");

            migrationBuilder.CreateIndex(
                name: "IX_DdsSemanais_ObraId",
                table: "DdsSemanais",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_DdsSemanais_ResponsavelUsuarioId",
                table: "DdsSemanais",
                column: "ResponsavelUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_DdsSemanais_ResponsavelObraSstUsuarioId",
                table: "DdsSemanais",
                column: "ResponsavelObraSstUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_DdsSemanais_ObraId_Tipo_DataInicioSemana",
                table: "DdsSemanais",
                columns: new[] { "ObraId", "Tipo", "DataInicioSemana" });

            migrationBuilder.CreateIndex(
                name: "IX_DdsFotosEvidencia_DdsId",
                table: "DdsFotosEvidencia",
                column: "DdsId");

            migrationBuilder.AddForeignKey(
                name: "FK_Dds_DdsSemanais_DdsSemanalId",
                table: "Dds",
                column: "DdsSemanalId",
                principalTable: "DdsSemanais",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Dds_CatalogosTemaDds_CatalogoTemaDdsId",
                table: "Dds",
                column: "CatalogoTemaDdsId",
                principalTable: "CatalogosTemaDds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Dds_DdsSemanais_DdsSemanalId",
                table: "Dds");

            migrationBuilder.DropForeignKey(
                name: "FK_Dds_CatalogosTemaDds_CatalogoTemaDdsId",
                table: "Dds");

            migrationBuilder.DropTable(
                name: "DdsFotosEvidencia");

            migrationBuilder.DropTable(
                name: "DdsSemanais");

            migrationBuilder.DropTable(
                name: "CatalogosTemaDds");

            migrationBuilder.DropIndex(
                name: "IX_Dds_DdsSemanalId",
                table: "Dds");

            migrationBuilder.DropIndex(
                name: "IX_Dds_CatalogoTemaDdsId",
                table: "Dds");

            migrationBuilder.DropColumn(
                name: "DdsSemanalId",
                table: "Dds");

            migrationBuilder.DropColumn(
                name: "OrigemTema",
                table: "Dds");

            migrationBuilder.DropColumn(
                name: "CatalogoTemaDdsId",
                table: "Dds");

            migrationBuilder.DropColumn(
                name: "Ordem",
                table: "DdsAtividades");
        }
    }
}
