using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CorrigirRowVersionAcidentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Escopo desta migration: somente Acidentes.RowVersion (bloqueava a criação de
            // qualquer acidente — 500 "Cannot insert an explicit value into a timestamp column").
            // Trabalhadores/Permissoes/Funcoes têm o mesmo drift de snapshot (código já usa
            // IsRowVersion() mas o snapshot nunca foi atualizado) — deliberadamente fora do
            // escopo aqui: são tabelas compartilhadas por outros módulos/sessões em uso agora.
            // Ver comentário na migration AdicionarGravidadeEHhtMensal.
            //
            // SQL Server rejeita ALTER COLUMN direto para timestamp mesmo quando a coluna física
            // já é timestamp ("Cannot alter column 'RowVersion' to be data type timestamp",
            // erro 4927) — não é possível converter via ALTER COLUMN em nenhum sentido. A única
            // forma é dropar e recriar a coluna. Isso reseta o token de concorrência otimista de
            // linhas existentes (inofensivo — não afeta os dados do acidente em si).
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Acidentes");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Acidentes",
                type: "rowversion",
                rowVersion: true,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "Acidentes",
                type: "varbinary(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true,
                oldNullable: true);
        }
    }
}
