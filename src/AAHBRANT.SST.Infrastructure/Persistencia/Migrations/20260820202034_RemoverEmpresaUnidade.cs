using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class RemoverEmpresaUnidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MatrizRiscoConfigs_Empresas_EmpresaId",
                table: "MatrizRiscoConfigs");

            migrationBuilder.DropForeignKey(
                name: "FK_Obras_Unidades_UnidadeId",
                table: "Obras");

            migrationBuilder.DropForeignKey(
                name: "FK_Trabalhadores_Empresas_EmpresaId",
                table: "Trabalhadores");

            migrationBuilder.DropTable(
                name: "Unidades");

            migrationBuilder.DropTable(
                name: "Empresas");

            migrationBuilder.DropIndex(
                name: "IX_Trabalhadores_EmpresaId_Matricula",
                table: "Trabalhadores");

            migrationBuilder.DropIndex(
                name: "IX_Trabalhadores_ObraId",
                table: "Trabalhadores");

            migrationBuilder.DropIndex(
                name: "IX_Obras_UnidadeId",
                table: "Obras");

            migrationBuilder.DropIndex(
                name: "IX_MatrizRiscoConfigs_EmpresaId",
                table: "MatrizRiscoConfigs");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "UnidadeId",
                table: "Obras");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "MatrizRiscoConfigs");

            migrationBuilder.AlterColumn<Guid>(
                name: "ObraId",
                table: "Trabalhadores",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trabalhadores_ObraId_Matricula",
                table: "Trabalhadores",
                columns: new[] { "ObraId", "Matricula" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trabalhadores_ObraId_Matricula",
                table: "Trabalhadores");

            migrationBuilder.AlterColumn<Guid>(
                name: "ObraId",
                table: "Trabalhadores",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "EmpresaId",
                table: "Trabalhadores",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UnidadeId",
                table: "Obras",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "EmpresaId",
                table: "MatrizRiscoConfigs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Empresas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    Cnpj = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NomeFantasia = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    RazaoSocial = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empresas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Unidades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    Cidade = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    Uf = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Unidades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Unidades_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Trabalhadores_EmpresaId_Matricula",
                table: "Trabalhadores",
                columns: new[] { "EmpresaId", "Matricula" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trabalhadores_ObraId",
                table: "Trabalhadores",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_Obras_UnidadeId",
                table: "Obras",
                column: "UnidadeId");

            migrationBuilder.CreateIndex(
                name: "IX_MatrizRiscoConfigs_EmpresaId",
                table: "MatrizRiscoConfigs",
                column: "EmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_Empresas_Cnpj",
                table: "Empresas",
                column: "Cnpj",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Unidades_EmpresaId",
                table: "Unidades",
                column: "EmpresaId");

            migrationBuilder.AddForeignKey(
                name: "FK_MatrizRiscoConfigs_Empresas_EmpresaId",
                table: "MatrizRiscoConfigs",
                column: "EmpresaId",
                principalTable: "Empresas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Obras_Unidades_UnidadeId",
                table: "Obras",
                column: "UnidadeId",
                principalTable: "Unidades",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Trabalhadores_Empresas_EmpresaId",
                table: "Trabalhadores",
                column: "EmpresaId",
                principalTable: "Empresas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
