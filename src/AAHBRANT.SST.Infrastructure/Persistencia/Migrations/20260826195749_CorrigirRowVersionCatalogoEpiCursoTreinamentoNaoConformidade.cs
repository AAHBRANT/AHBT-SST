using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CorrigirRowVersionCatalogoEpiCursoTreinamentoNaoConformidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL Server rejeita ALTER COLUMN direto para timestamp/rowversion mesmo vindo de
            // varbinary(max) ("Cannot alter column 'RowVersion' to be data type timestamp", erro
            // 4927) — mesma limitação documentada na migration CorrigirRowVersionAcidentes. A única
            // forma é dropar e recriar a coluna. Isso reseta o token de concorrência otimista de
            // linhas existentes (inofensivo em Development/hml — sem linhas reais dependendo dele).
            migrationBuilder.DropColumn(name: "RowVersion", table: "NaoConformidades");
            migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "NaoConformidades", type: "rowversion", rowVersion: true, nullable: true);

            migrationBuilder.DropColumn(name: "RowVersion", table: "CursosTreinamento");
            migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "CursosTreinamento", type: "rowversion", rowVersion: true, nullable: true);

            migrationBuilder.DropColumn(name: "RowVersion", table: "CatalogoEpis");
            migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "CatalogoEpis", type: "rowversion", rowVersion: true, nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RowVersion", table: "NaoConformidades");
            migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "NaoConformidades", type: "varbinary(max)", nullable: true);

            migrationBuilder.DropColumn(name: "RowVersion", table: "CursosTreinamento");
            migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "CursosTreinamento", type: "varbinary(max)", nullable: true);

            migrationBuilder.DropColumn(name: "RowVersion", table: "CatalogoEpis");
            migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "CatalogoEpis", type: "varbinary(max)", nullable: true);
        }
    }
}
