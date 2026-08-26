using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CorrigirRowVersionObraSetorEquipeAreaSst : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL Server rejeita ALTER COLUMN direto para timestamp/rowversion mesmo vindo de
            // varbinary(max) ("Cannot alter column 'RowVersion' to be data type timestamp", erro
            // 4927) — mesma limitação documentada na migration CorrigirRowVersionAcidentes. A única
            // forma é dropar e recriar a coluna. Isso reseta o token de concorrência otimista de
            // linhas existentes (inofensivo em Development/hml — sem linhas reais dependendo dele).
            migrationBuilder.DropColumn(name: "RowVersion", table: "Setores");
            migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "Setores", type: "rowversion", rowVersion: true, nullable: true);

            migrationBuilder.DropColumn(name: "RowVersion", table: "Obras");
            migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "Obras", type: "rowversion", rowVersion: true, nullable: true);

            migrationBuilder.DropColumn(name: "RowVersion", table: "Equipes");
            migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "Equipes", type: "rowversion", rowVersion: true, nullable: true);

            migrationBuilder.DropColumn(name: "RowVersion", table: "AreasSst");
            migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "AreasSst", type: "rowversion", rowVersion: true, nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RowVersion", table: "Setores");
            migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "Setores", type: "varbinary(max)", nullable: true);

            migrationBuilder.DropColumn(name: "RowVersion", table: "Obras");
            migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "Obras", type: "varbinary(max)", nullable: true);

            migrationBuilder.DropColumn(name: "RowVersion", table: "Equipes");
            migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "Equipes", type: "varbinary(max)", nullable: true);

            migrationBuilder.DropColumn(name: "RowVersion", table: "AreasSst");
            migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "AreasSst", type: "varbinary(max)", nullable: true);
        }
    }
}
