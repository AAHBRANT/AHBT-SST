using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarGravidadeEHhtMensal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTA: as colunas RowVersion de Trabalhadores/Permissoes/Funcoes já estão marcadas como
            // IsRowVersion() no código (config de concorrência otimista) mas o SQL Server rejeita
            // ALTER COLUMN direto de varbinary(max) para rowversion ("Cannot alter column 'RowVersion'
            // to be data type timestamp"). Corrigir esse drift pré-existente exige uma migration própria
            // (dropar/recriar a coluna) e está fora do escopo desta feature — removido daqui
            // deliberadamente para não travar o deploy da Taxa de Gravidade.
            migrationBuilder.AddColumn<int>(
                name: "DiasDebitados",
                table: "Acidentes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Gravidade",
                table: "Acidentes",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "RegistrosHhtMensais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ano = table.Column<int>(type: "int", nullable: false),
                    Mes = table.Column<int>(type: "int", nullable: false),
                    HorasHomemTrabalhadas = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_RegistrosHhtMensais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrosHhtMensais_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosHhtMensais_ObraId_Ano_Mes",
                table: "RegistrosHhtMensais",
                columns: new[] { "ObraId", "Ano", "Mes" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegistrosHhtMensais");

            migrationBuilder.DropColumn(
                name: "DiasDebitados",
                table: "Acidentes");

            migrationBuilder.DropColumn(
                name: "Gravidade",
                table: "Acidentes");
        }
    }
}
