using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class RemoverHigienizacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegistrosHigienizacao");

            migrationBuilder.DropTable(
                name: "ItensHigienizacao");

            // SQL Server rejeita ALTER COLUMN direto para timestamp/rowversion, mesmo quando a
            // coluna física já é desse tipo (erro 4927 "Cannot alter column ... to be data type
            // timestamp") — não é possível converter via ALTER COLUMN em nenhum sentido. A única
            // forma é dropar e recriar a coluna. Isso reseta o token de concorrência otimista de
            // linhas existentes (inofensivo). Mesmo padrão de CorrigirRowVersionAcidentes.
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Trabalhadores");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Trabalhadores",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Permissoes");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Permissoes",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Funcoes");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Funcoes",
                type: "rowversion",
                rowVersion: true,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "Trabalhadores",
                type: "varbinary(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true,
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "Permissoes",
                type: "varbinary(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true,
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "Funcoes",
                type: "varbinary(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ItensHigienizacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Local = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    PeriodicidadeDias = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensHigienizacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensHigienizacao_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegistrosHigienizacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemHigienizacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FotoContentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FotoConteudo = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Observacoes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosHigienizacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrosHigienizacao_ItensHigienizacao_ItemHigienizacaoId",
                        column: x => x.ItemHigienizacaoId,
                        principalTable: "ItensHigienizacao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RegistrosHigienizacao_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItensHigienizacao_ObraId",
                table: "ItensHigienizacao",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosHigienizacao_ItemHigienizacaoId",
                table: "RegistrosHigienizacao",
                column: "ItemHigienizacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosHigienizacao_TrabalhadorId",
                table: "RegistrosHigienizacao",
                column: "TrabalhadorId");
        }
    }
}
