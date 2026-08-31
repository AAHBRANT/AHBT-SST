using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class ReformularPtLiteralREV01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PermissaoTrabalhoControles");

            migrationBuilder.DropTable(
                name: "PermissaoTrabalhoPerigos");

            migrationBuilder.DropTable(
                name: "PermissaoTrabalhoRequisitos");

            migrationBuilder.AddColumn<DateTime>(
                name: "DataAssinaturaExecucao",
                table: "PermissoesTrabalho",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataAssinaturaSst",
                table: "PermissoesTrabalho",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataRevalidacao",
                table: "PermissoesTrabalho",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataSuspensao",
                table: "PermissoesTrabalho",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescricaoAtividade",
                table: "PermissoesTrabalho",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmpresaExecutante",
                table: "PermissoesTrabalho",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoSuspensao",
                table: "PermissoesTrabalho",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroPt",
                table: "PermissoesTrabalho",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutrosEpcs",
                table: "PermissoesTrabalho",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutrosEpis",
                table: "PermissoesTrabalho",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResponsavelAreaUsuarioId",
                table: "PermissoesTrabalho",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResponsavelExecucaoUsuarioId",
                table: "PermissoesTrabalho",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResponsavelSstUsuarioId",
                table: "PermissoesTrabalho",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RevalidadaPorUsuarioId",
                table: "PermissoesTrabalho",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SuspensaPorUsuarioId",
                table: "PermissoesTrabalho",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PermissaoTrabalhoEpcs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissaoTrabalhoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Item = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_PermissaoTrabalhoEpcs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermissaoTrabalhoEpcs_PermissoesTrabalho_PermissaoTrabalhoId",
                        column: x => x.PermissaoTrabalhoId,
                        principalTable: "PermissoesTrabalho",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PermissaoTrabalhoEpis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissaoTrabalhoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Item = table.Column<int>(type: "int", nullable: false),
                    Complemento = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_PermissaoTrabalhoEpis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermissaoTrabalhoEpis_PermissoesTrabalho_PermissaoTrabalhoId",
                        column: x => x.PermissaoTrabalhoId,
                        principalTable: "PermissoesTrabalho",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PermissaoTrabalhoPreRequisitos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissaoTrabalhoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Item = table.Column<int>(type: "int", nullable: false),
                    Atendido = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_PermissaoTrabalhoPreRequisitos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermissaoTrabalhoPreRequisitos_PermissoesTrabalho_PermissaoTrabalhoId",
                        column: x => x.PermissaoTrabalhoId,
                        principalTable: "PermissoesTrabalho",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PermissaoTrabalhoRiscosCriticos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissaoTrabalhoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiscoCondicao = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ControleComplementar = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResponsavelEvidencia = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_PermissaoTrabalhoRiscosCriticos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermissaoTrabalhoRiscosCriticos_PermissoesTrabalho_PermissaoTrabalhoId",
                        column: x => x.PermissaoTrabalhoId,
                        principalTable: "PermissoesTrabalho",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PermissaoTrabalhoTiposTrabalho",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissaoTrabalhoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    DescricaoOutro = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_PermissaoTrabalhoTiposTrabalho", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermissaoTrabalhoTiposTrabalho_PermissoesTrabalho_PermissaoTrabalhoId",
                        column: x => x.PermissaoTrabalhoId,
                        principalTable: "PermissoesTrabalho",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PermissaoTrabalhoVerificacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissaoTrabalhoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Item = table.Column<int>(type: "int", nullable: false),
                    Resposta = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_PermissaoTrabalhoVerificacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermissaoTrabalhoVerificacoes_PermissoesTrabalho_PermissaoTrabalhoId",
                        column: x => x.PermissaoTrabalhoId,
                        principalTable: "PermissoesTrabalho",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PermissoesTrabalho_ResponsavelAreaUsuarioId",
                table: "PermissoesTrabalho",
                column: "ResponsavelAreaUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissoesTrabalho_ResponsavelExecucaoUsuarioId",
                table: "PermissoesTrabalho",
                column: "ResponsavelExecucaoUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissoesTrabalho_ResponsavelSstUsuarioId",
                table: "PermissoesTrabalho",
                column: "ResponsavelSstUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissoesTrabalho_RevalidadaPorUsuarioId",
                table: "PermissoesTrabalho",
                column: "RevalidadaPorUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissoesTrabalho_SuspensaPorUsuarioId",
                table: "PermissoesTrabalho",
                column: "SuspensaPorUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissaoTrabalhoEpcs_PermissaoTrabalhoId_Item",
                table: "PermissaoTrabalhoEpcs",
                columns: new[] { "PermissaoTrabalhoId", "Item" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermissaoTrabalhoEpis_PermissaoTrabalhoId_Item",
                table: "PermissaoTrabalhoEpis",
                columns: new[] { "PermissaoTrabalhoId", "Item" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermissaoTrabalhoPreRequisitos_PermissaoTrabalhoId_Item",
                table: "PermissaoTrabalhoPreRequisitos",
                columns: new[] { "PermissaoTrabalhoId", "Item" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermissaoTrabalhoRiscosCriticos_PermissaoTrabalhoId",
                table: "PermissaoTrabalhoRiscosCriticos",
                column: "PermissaoTrabalhoId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissaoTrabalhoTiposTrabalho_PermissaoTrabalhoId_Tipo",
                table: "PermissaoTrabalhoTiposTrabalho",
                columns: new[] { "PermissaoTrabalhoId", "Tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermissaoTrabalhoVerificacoes_PermissaoTrabalhoId_Item",
                table: "PermissaoTrabalhoVerificacoes",
                columns: new[] { "PermissaoTrabalhoId", "Item" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PermissoesTrabalho_Usuarios_ResponsavelAreaUsuarioId",
                table: "PermissoesTrabalho",
                column: "ResponsavelAreaUsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PermissoesTrabalho_Usuarios_ResponsavelExecucaoUsuarioId",
                table: "PermissoesTrabalho",
                column: "ResponsavelExecucaoUsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PermissoesTrabalho_Usuarios_ResponsavelSstUsuarioId",
                table: "PermissoesTrabalho",
                column: "ResponsavelSstUsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PermissoesTrabalho_Usuarios_RevalidadaPorUsuarioId",
                table: "PermissoesTrabalho",
                column: "RevalidadaPorUsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PermissoesTrabalho_Usuarios_SuspensaPorUsuarioId",
                table: "PermissoesTrabalho",
                column: "SuspensaPorUsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PermissoesTrabalho_Usuarios_ResponsavelAreaUsuarioId",
                table: "PermissoesTrabalho");

            migrationBuilder.DropForeignKey(
                name: "FK_PermissoesTrabalho_Usuarios_ResponsavelExecucaoUsuarioId",
                table: "PermissoesTrabalho");

            migrationBuilder.DropForeignKey(
                name: "FK_PermissoesTrabalho_Usuarios_ResponsavelSstUsuarioId",
                table: "PermissoesTrabalho");

            migrationBuilder.DropForeignKey(
                name: "FK_PermissoesTrabalho_Usuarios_RevalidadaPorUsuarioId",
                table: "PermissoesTrabalho");

            migrationBuilder.DropForeignKey(
                name: "FK_PermissoesTrabalho_Usuarios_SuspensaPorUsuarioId",
                table: "PermissoesTrabalho");

            migrationBuilder.DropTable(
                name: "PermissaoTrabalhoEpcs");

            migrationBuilder.DropTable(
                name: "PermissaoTrabalhoEpis");

            migrationBuilder.DropTable(
                name: "PermissaoTrabalhoPreRequisitos");

            migrationBuilder.DropTable(
                name: "PermissaoTrabalhoRiscosCriticos");

            migrationBuilder.DropTable(
                name: "PermissaoTrabalhoTiposTrabalho");

            migrationBuilder.DropTable(
                name: "PermissaoTrabalhoVerificacoes");

            migrationBuilder.DropIndex(
                name: "IX_PermissoesTrabalho_ResponsavelAreaUsuarioId",
                table: "PermissoesTrabalho");

            migrationBuilder.DropIndex(
                name: "IX_PermissoesTrabalho_ResponsavelExecucaoUsuarioId",
                table: "PermissoesTrabalho");

            migrationBuilder.DropIndex(
                name: "IX_PermissoesTrabalho_ResponsavelSstUsuarioId",
                table: "PermissoesTrabalho");

            migrationBuilder.DropIndex(
                name: "IX_PermissoesTrabalho_RevalidadaPorUsuarioId",
                table: "PermissoesTrabalho");

            migrationBuilder.DropIndex(
                name: "IX_PermissoesTrabalho_SuspensaPorUsuarioId",
                table: "PermissoesTrabalho");

            migrationBuilder.DropColumn(
                name: "DataAssinaturaExecucao",
                table: "PermissoesTrabalho");

            migrationBuilder.DropColumn(
                name: "DataAssinaturaSst",
                table: "PermissoesTrabalho");

            migrationBuilder.DropColumn(
                name: "DataRevalidacao",
                table: "PermissoesTrabalho");

            migrationBuilder.DropColumn(
                name: "DataSuspensao",
                table: "PermissoesTrabalho");

            migrationBuilder.DropColumn(
                name: "DescricaoAtividade",
                table: "PermissoesTrabalho");

            migrationBuilder.DropColumn(
                name: "EmpresaExecutante",
                table: "PermissoesTrabalho");

            migrationBuilder.DropColumn(
                name: "MotivoSuspensao",
                table: "PermissoesTrabalho");

            migrationBuilder.DropColumn(
                name: "NumeroPt",
                table: "PermissoesTrabalho");

            migrationBuilder.DropColumn(
                name: "OutrosEpcs",
                table: "PermissoesTrabalho");

            migrationBuilder.DropColumn(
                name: "OutrosEpis",
                table: "PermissoesTrabalho");

            migrationBuilder.DropColumn(
                name: "ResponsavelAreaUsuarioId",
                table: "PermissoesTrabalho");

            migrationBuilder.DropColumn(
                name: "ResponsavelExecucaoUsuarioId",
                table: "PermissoesTrabalho");

            migrationBuilder.DropColumn(
                name: "ResponsavelSstUsuarioId",
                table: "PermissoesTrabalho");

            migrationBuilder.DropColumn(
                name: "RevalidadaPorUsuarioId",
                table: "PermissoesTrabalho");

            migrationBuilder.DropColumn(
                name: "SuspensaPorUsuarioId",
                table: "PermissoesTrabalho");

            migrationBuilder.CreateTable(
                name: "PermissaoTrabalhoControles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissaoTrabalhoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissaoTrabalhoControles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermissaoTrabalhoControles_PermissoesTrabalho_PermissaoTrabalhoId",
                        column: x => x.PermissaoTrabalhoId,
                        principalTable: "PermissoesTrabalho",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PermissaoTrabalhoPerigos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerigoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissaoTrabalhoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissaoTrabalhoPerigos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermissaoTrabalhoPerigos_Perigos_PerigoId",
                        column: x => x.PerigoId,
                        principalTable: "Perigos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PermissaoTrabalhoPerigos_PermissoesTrabalho_PermissaoTrabalhoId",
                        column: x => x.PermissaoTrabalhoId,
                        principalTable: "PermissoesTrabalho",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PermissaoTrabalhoRequisitos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissaoTrabalhoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Atendido = table.Column<bool>(type: "bit", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissaoTrabalhoRequisitos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermissaoTrabalhoRequisitos_PermissoesTrabalho_PermissaoTrabalhoId",
                        column: x => x.PermissaoTrabalhoId,
                        principalTable: "PermissoesTrabalho",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PermissaoTrabalhoControles_PermissaoTrabalhoId",
                table: "PermissaoTrabalhoControles",
                column: "PermissaoTrabalhoId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissaoTrabalhoPerigos_PerigoId",
                table: "PermissaoTrabalhoPerigos",
                column: "PerigoId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissaoTrabalhoPerigos_PermissaoTrabalhoId_PerigoId",
                table: "PermissaoTrabalhoPerigos",
                columns: new[] { "PermissaoTrabalhoId", "PerigoId" });

            migrationBuilder.CreateIndex(
                name: "IX_PermissaoTrabalhoRequisitos_PermissaoTrabalhoId",
                table: "PermissaoTrabalhoRequisitos",
                column: "PermissaoTrabalhoId");
        }
    }
}
