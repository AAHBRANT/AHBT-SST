using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarOcorrenciaPessoaExterna : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Papel",
                table: "AcidentesEnvolvidos",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "AguardandoLaudoDesde",
                table: "Acidentes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConclusaoInvestigacao",
                table: "Acidentes",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LaudoObservacao",
                table: "Acidentes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvidenciasObitoJson",
                table: "Acidentes",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AcidentesPessoasExternas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcidenteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Vinculo = table.Column<int>(type: "int", nullable: false),
                    Empresa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Cpf = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Telefone = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Idade = table.Column<int>(type: "int", nullable: true),
                    Lesao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Atendimento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Resultado = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_AcidentesPessoasExternas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcidentesPessoasExternas_Acidentes_AcidenteId",
                        column: x => x.AcidenteId,
                        principalTable: "Acidentes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcidentesPessoasExternas_AcidenteId",
                table: "AcidentesPessoasExternas",
                column: "AcidenteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcidentesPessoasExternas");

            migrationBuilder.DropColumn(
                name: "Papel",
                table: "AcidentesEnvolvidos");

            migrationBuilder.DropColumn(
                name: "AguardandoLaudoDesde",
                table: "Acidentes");

            migrationBuilder.DropColumn(
                name: "ConclusaoInvestigacao",
                table: "Acidentes");

            migrationBuilder.DropColumn(
                name: "LaudoObservacao",
                table: "Acidentes");

            migrationBuilder.DropColumn(
                name: "ProvidenciasObitoJson",
                table: "Acidentes");
        }
    }
}
