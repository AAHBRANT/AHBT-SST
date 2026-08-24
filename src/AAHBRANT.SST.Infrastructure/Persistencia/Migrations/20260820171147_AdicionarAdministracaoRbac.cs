using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarAdministracaoRbac : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PerfisAcessoPermissoes_PerfilAcessoId_Modulo_Acao_Escopo",
                table: "PerfisAcessoPermissoes");

            migrationBuilder.DropIndex(
                name: "IX_PerfisAcesso_Tipo",
                table: "PerfisAcesso");

            migrationBuilder.DropColumn(
                name: "Acao",
                table: "PerfisAcessoPermissoes");

            migrationBuilder.DropColumn(
                name: "Modulo",
                table: "PerfisAcessoPermissoes");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Usuarios",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimoLoginUtc",
                table: "Usuarios",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PermissaoId",
                table: "PerfisAcessoPermissoes",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<int>(
                name: "Tipo",
                table: "PerfisAcesso",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<bool>(
                name: "EhSistema",
                table: "PerfisAcesso",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Permissoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Modulo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Acao = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
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
                    table.PrimaryKey("PK_Permissoes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PerfisAcessoPermissoes_PerfilAcessoId_PermissaoId_Escopo",
                table: "PerfisAcessoPermissoes",
                columns: new[] { "PerfilAcessoId", "PermissaoId", "Escopo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PerfisAcessoPermissoes_PermissaoId",
                table: "PerfisAcessoPermissoes",
                column: "PermissaoId");

            migrationBuilder.CreateIndex(
                name: "IX_PerfisAcesso_Tipo",
                table: "PerfisAcesso",
                column: "Tipo",
                unique: true,
                filter: "[Tipo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Permissoes_Codigo",
                table: "Permissoes",
                column: "Codigo",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PerfisAcessoPermissoes_Permissoes_PermissaoId",
                table: "PerfisAcessoPermissoes",
                column: "PermissaoId",
                principalTable: "Permissoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PerfisAcessoPermissoes_Permissoes_PermissaoId",
                table: "PerfisAcessoPermissoes");

            migrationBuilder.DropTable(
                name: "Permissoes");

            migrationBuilder.DropIndex(
                name: "IX_PerfisAcessoPermissoes_PerfilAcessoId_PermissaoId_Escopo",
                table: "PerfisAcessoPermissoes");

            migrationBuilder.DropIndex(
                name: "IX_PerfisAcessoPermissoes_PermissaoId",
                table: "PerfisAcessoPermissoes");

            migrationBuilder.DropIndex(
                name: "IX_PerfisAcesso_Tipo",
                table: "PerfisAcesso");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "UltimoLoginUtc",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "PermissaoId",
                table: "PerfisAcessoPermissoes");

            migrationBuilder.DropColumn(
                name: "EhSistema",
                table: "PerfisAcesso");

            migrationBuilder.AddColumn<string>(
                name: "Acao",
                table: "PerfisAcessoPermissoes",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Modulo",
                table: "PerfisAcessoPermissoes",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "Tipo",
                table: "PerfisAcesso",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PerfisAcessoPermissoes_PerfilAcessoId_Modulo_Acao_Escopo",
                table: "PerfisAcessoPermissoes",
                columns: new[] { "PerfilAcessoId", "Modulo", "Acao", "Escopo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PerfisAcesso_Tipo",
                table: "PerfisAcesso",
                column: "Tipo",
                unique: true);
        }
    }
}
