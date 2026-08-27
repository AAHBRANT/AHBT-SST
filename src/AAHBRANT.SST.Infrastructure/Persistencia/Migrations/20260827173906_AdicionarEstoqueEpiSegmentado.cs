using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarEstoqueEpiSegmentado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EstoquesEpi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogoEpiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Saldo = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_EstoquesEpi", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EstoquesEpi_CatalogoEpis_CatalogoEpiId",
                        column: x => x.CatalogoEpiId,
                        principalTable: "CatalogoEpis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EstoquesEpi_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimentacoesEstoqueEpi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EstoqueEpiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Quantidade = table.Column<int>(type: "int", nullable: false),
                    SaldoResultante = table.Column<int>(type: "int", nullable: false),
                    EntregaEpiId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Observacao = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_MovimentacoesEstoqueEpi", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimentacoesEstoqueEpi_EntregasEpi_EntregaEpiId",
                        column: x => x.EntregaEpiId,
                        principalTable: "EntregasEpi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimentacoesEstoqueEpi_EstoquesEpi_EstoqueEpiId",
                        column: x => x.EstoqueEpiId,
                        principalTable: "EstoquesEpi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EstoquesEpi_CatalogoEpiId_ObraId",
                table: "EstoquesEpi",
                columns: new[] { "CatalogoEpiId", "ObraId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EstoquesEpi_ObraId",
                table: "EstoquesEpi",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesEstoqueEpi_EntregaEpiId",
                table: "MovimentacoesEstoqueEpi",
                column: "EntregaEpiId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesEstoqueEpi_EstoqueEpiId_CreatedAtUtc",
                table: "MovimentacoesEstoqueEpi",
                columns: new[] { "EstoqueEpiId", "CreatedAtUtc" });

            // Backfill: antes do estoque ser segmentado por Obra, CatalogoEpi.SaldoEstoque era um
            // saldo único global. Só é seguro migrar esse valor automaticamente quando existe
            // exatamente 1 Obra no banco (caso do seeder de obra mocada e, hoje, também do estado
            // real em homologação) — nesse caso o saldo antigo pertence, sem ambiguidade, a essa
            // Obra. Com 0 ou 2+ Obras não há como saber a qual Obra o saldo global pertencia; nesse
            // caso a migração não cria EstoqueEpi nenhum e o saldo por Obra começa em 0, cabendo a
            // um administrador lançar o estoque real pela nova tela.
            migrationBuilder.Sql(@"
IF (SELECT COUNT(*) FROM Obras) = 1
BEGIN
    INSERT INTO EstoquesEpi (Id, CatalogoEpiId, ObraId, Saldo, CreatedAtUtc, Origem, Ativo)
    SELECT NEWID(), c.Id, o.Id, c.SaldoEstoque, SYSUTCDATETIME(), 0, 1
    FROM CatalogoEpis c
    CROSS JOIN Obras o
    WHERE c.Ativo = 1;
END
");

            migrationBuilder.DropColumn(
                name: "SaldoEstoque",
                table: "CatalogoEpis");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovimentacoesEstoqueEpi");

            migrationBuilder.DropTable(
                name: "EstoquesEpi");

            migrationBuilder.AddColumn<int>(
                name: "SaldoEstoque",
                table: "CatalogoEpis",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
