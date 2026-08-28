using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class RemoverMatrizLegalEGestaoDocumental : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Limpeza de dados órfãos deixados pela remoção do módulo de Conformidade (Matriz Legal +
            // Gestão Documental): AcoesPlano é uma tabela genérica/polimórfica (OrigemTipo/OrigemId,
            // sem FK real) reutilizada por NaoConformidade/Acidente/RequisitoLegal — remove apenas as
            // linhas que apontavam para RequisitoLegal. RbacSeeder só insere permissões que faltam
            // (nunca remove as que saíram do catálogo), então também limpa as permissões do módulo
            // removido e os vínculos de perfil que as usavam.
            migrationBuilder.Sql(
                "DELETE FROM [AcoesPlano] WHERE [OrigemTipo] = N'RequisitoLegal';");

            migrationBuilder.Sql(@"
                DELETE ppp
                FROM [PerfisAcessoPermissoes] ppp
                INNER JOIN [Permissoes] p ON p.[Id] = ppp.[PermissaoId]
                WHERE p.[Codigo] IN (
                    N'matrizlegal:ver', N'matrizlegal:criar', N'matrizlegal:editar', N'matrizlegal:atualizar_status',
                    N'documento:ver', N'documento:criar', N'documento:editar', N'documento:atualizar_status', N'documento:revisar'
                );");

            migrationBuilder.Sql(@"
                DELETE FROM [Permissoes]
                WHERE [Codigo] IN (
                    N'matrizlegal:ver', N'matrizlegal:criar', N'matrizlegal:editar', N'matrizlegal:atualizar_status',
                    N'documento:ver', N'documento:criar', N'documento:editar', N'documento:atualizar_status', N'documento:revisar'
                );");

            migrationBuilder.DropForeignKey(
                name: "FK_PcmsoDetalhes_DocumentosGestao_DocumentoGestaoId",
                table: "PcmsoDetalhes");

            migrationBuilder.DropTable(
                name: "DocumentoRevisoes");

            migrationBuilder.DropTable(
                name: "DocumentosGestao");

            migrationBuilder.DropTable(
                name: "RequisitosLegais");

            // SQL Server rejeita ALTER COLUMN direto para timestamp/rowversion mesmo vindo de
            // varbinary(max) ("Cannot alter column 'RowVersion' to be data type timestamp", erro
            // 4927) — mesma limitação já corrigida em CorrigirRowVersionAcaoPlano/
            // CorrigirRowVersionDocumentoGestao/etc. A única forma é dropar e recriar a coluna. Isso
            // reseta o token de concorrência otimista de linhas existentes (inofensivo em
            // Development/hml — sem linhas reais dependendo dele).
            migrationBuilder.DropColumn(name: "RowVersion", table: "TemplatesBiometricoFutronic");
            migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "TemplatesBiometricoFutronic", type: "rowversion", rowVersion: true, nullable: true);

            migrationBuilder.DropColumn(name: "RowVersion", table: "RegrasAlerta");
            migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "RegrasAlerta", type: "rowversion", rowVersion: true, nullable: true);

            migrationBuilder.DropColumn(name: "RowVersion", table: "RegistrosHhtMensais");
            migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "RegistrosHhtMensais", type: "rowversion", rowVersion: true, nullable: true);

            migrationBuilder.DropColumn(name: "RowVersion", table: "DocumentoSignatarios");
            migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "DocumentoSignatarios", type: "rowversion", rowVersion: true, nullable: true);

            migrationBuilder.DropColumn(name: "RowVersion", table: "DocumentosAssinatura");
            migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "DocumentosAssinatura", type: "rowversion", rowVersion: true, nullable: true);

            migrationBuilder.DropColumn(name: "RowVersion", table: "DispositivosAgenteBiometrico");
            migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "DispositivosAgenteBiometrico", type: "rowversion", rowVersion: true, nullable: true);

            migrationBuilder.DropColumn(name: "RowVersion", table: "CredenciaisWebAuthn");
            migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "CredenciaisWebAuthn", type: "rowversion", rowVersion: true, nullable: true);

            migrationBuilder.DropColumn(name: "RowVersion", table: "CalendariosEventosTeams");
            migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "CalendariosEventosTeams", type: "rowversion", rowVersion: true, nullable: true);

            migrationBuilder.DropColumn(name: "RowVersion", table: "AtivosSst");
            migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "AtivosSst", type: "rowversion", rowVersion: true, nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "TemplatesBiometricoFutronic",
                type: "varbinary(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true,
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "RegrasAlerta",
                type: "varbinary(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true,
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "RegistrosHhtMensais",
                type: "varbinary(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true,
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "DocumentoSignatarios",
                type: "varbinary(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true,
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "DocumentosAssinatura",
                type: "varbinary(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true,
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "DispositivosAgenteBiometrico",
                type: "varbinary(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true,
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "CredenciaisWebAuthn",
                type: "varbinary(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true,
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "CalendariosEventosTeams",
                type: "varbinary(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true,
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "AtivosSst",
                type: "varbinary(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "RequisitosLegais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResponsavelUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Aplicabilidade = table.Column<bool>(type: "bit", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Evidencia = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Item = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Justificativa = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Norma = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    Periodicidade = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Prazo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProximaRevisao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Requisito = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Tema = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UltimaRevisao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequisitosLegais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequisitosLegais_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequisitosLegais_Usuarios_ResponsavelUsuarioId",
                        column: x => x.ResponsavelUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentosGestao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequisitoLegalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResponsavelUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SetorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Arquivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataEmissao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataRevisao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    OrigemDocumento = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Validade = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentosGestao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentosGestao_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentosGestao_RequisitosLegais_RequisitoLegalId",
                        column: x => x.RequisitoLegalId,
                        principalTable: "RequisitosLegais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentosGestao_Setores_SetorId",
                        column: x => x.SetorId,
                        principalTable: "Setores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentosGestao_Usuarios_ResponsavelUsuarioId",
                        column: x => x.ResponsavelUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentoRevisoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponsavelUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataRevisao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    NumeroRevisao = table.Column<int>(type: "int", nullable: false),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentoRevisoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentoRevisoes_DocumentosGestao_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "DocumentosGestao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentoRevisoes_Usuarios_ResponsavelUsuarioId",
                        column: x => x.ResponsavelUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoRevisoes_DocumentoId",
                table: "DocumentoRevisoes",
                column: "DocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentoRevisoes_ResponsavelUsuarioId",
                table: "DocumentoRevisoes",
                column: "ResponsavelUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosGestao_Categoria",
                table: "DocumentosGestao",
                column: "Categoria");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosGestao_ObraId",
                table: "DocumentosGestao",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosGestao_RequisitoLegalId",
                table: "DocumentosGestao",
                column: "RequisitoLegalId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosGestao_ResponsavelUsuarioId",
                table: "DocumentosGestao",
                column: "ResponsavelUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosGestao_SetorId",
                table: "DocumentosGestao",
                column: "SetorId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosGestao_Status",
                table: "DocumentosGestao",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosGestao_Tipo",
                table: "DocumentosGestao",
                column: "Tipo");

            migrationBuilder.CreateIndex(
                name: "IX_RequisitosLegais_Norma",
                table: "RequisitosLegais",
                column: "Norma");

            migrationBuilder.CreateIndex(
                name: "IX_RequisitosLegais_ObraId",
                table: "RequisitosLegais",
                column: "ObraId");

            migrationBuilder.CreateIndex(
                name: "IX_RequisitosLegais_ResponsavelUsuarioId",
                table: "RequisitosLegais",
                column: "ResponsavelUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_RequisitosLegais_Status",
                table: "RequisitosLegais",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_PcmsoDetalhes_DocumentosGestao_DocumentoGestaoId",
                table: "PcmsoDetalhes",
                column: "DocumentoGestaoId",
                principalTable: "DocumentosGestao",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
