using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CorrigirRowVersionAcaoPlano : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL Server rejeita ALTER COLUMN direto para timestamp/rowversion mesmo vindo de
            // varbinary(max) ("Cannot alter column 'RowVersion' to be data type timestamp", erro
            // 4927) — mesma limitação documentada na migration CorrigirRowVersionAcidentes. A única
            // forma é dropar e recriar a coluna. Isso reseta o token de concorrência otimista de
            // linhas existentes (inofensivo em Development/hml — sem linhas reais dependendo dele).
            migrationBuilder.DropColumn(name: "RowVersion", table: "AcoesPlano");
            migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "AcoesPlano", type: "rowversion", rowVersion: true, nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "AcoesPlano",
                type: "varbinary(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true,
                oldNullable: true);
        }
    }
}
