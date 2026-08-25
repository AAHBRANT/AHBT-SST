using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarSincronizacaoRbacPcmso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "UsuariosPerfilObra");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "UsuariosPerfilObra",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Usuarios");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Usuarios",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "TrilhaAuditoria");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "TrilhaAuditoria",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Treinamentos");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Treinamentos",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

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
                table: "TagsIdentificacao");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "TagsIdentificacao",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Setores");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Setores",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "RiscoTrabalhadorExpostos");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "RiscoTrabalhadorExpostos",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Riscos");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Riscos",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "RequisitosLegais");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "RequisitosLegais",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PlanoAcaoItens");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PlanoAcaoItens",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Pgrs");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Pgrs",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PgrRevisoes");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PgrRevisoes",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PermissoesTrabalho");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PermissoesTrabalho",
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
                table: "PermissaoTrabalhoResponsaveis");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PermissaoTrabalhoResponsaveis",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PermissaoTrabalhoRequisitos");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PermissaoTrabalhoRequisitos",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PermissaoTrabalhoPerigos");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PermissaoTrabalhoPerigos",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PermissaoTrabalhoControles");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PermissaoTrabalhoControles",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Perigos");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Perigos",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PerfisAcessoPermissoes");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PerfisAcessoPermissoes",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PerfisAcesso");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PerfisAcesso",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Obras");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Obras",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "NaoConformidades");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "NaoConformidades",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "MatrizRiscoConfigs");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "MatrizRiscoConfigs",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "MatrizRiscoCelulas");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "MatrizRiscoCelulas",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Inspecoes");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Inspecoes",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "InspecaoItemRespostas");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "InspecaoItemRespostas",
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

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Evidencias");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Evidencias",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Equipes");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Equipes",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "EntregasEpi");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "EntregasEpi",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DocumentosGestao");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DocumentosGestao",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DocumentoRevisoes");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DocumentoRevisoes",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DdsTelegramEnvios");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DdsTelegramEnvios",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DdsParticipantes");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DdsParticipantes",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DdsItensChecklist");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DdsItensChecklist",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DdsAtividades");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DdsAtividades",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Dds");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Dds",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "CursosTreinamento");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "CursosTreinamento",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ChecklistModelos");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ChecklistModelos",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ChecklistModeloItens");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ChecklistModeloItens",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "CatalogoEpis");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "CatalogoEpis",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Atividades");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Atividades",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Asos");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Asos",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AsoRestricoes");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AsoRestricoes",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AreasSst");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AreasSst",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Aprs");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Aprs",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AprResponsaveis");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AprResponsaveis",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AprEtapas");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AprEtapas",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AprEtapaRiscos");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AprEtapaRiscos",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AprAssinaturas");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AprAssinaturas",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Alertas");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Alertas",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AlertaHistoricoEnvios");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AlertaHistoricoEnvios",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AcoesPlano");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AcoesPlano",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Acidentes");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Acidentes",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IdempotenciaRegistros",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Chave = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Rota = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    StatusCodeResposta = table.Column<int>(type: "int", nullable: false),
                    CorpoResposta = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_IdempotenciaRegistros", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pcmsos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Objetivo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MedicoCoordenadorNome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MedicoCoordenadorCrm = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MedicoCoordenadorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataElaboracao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataVigenciaInicio = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataVigenciaFim = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_Pcmsos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pcmsos_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pcmsos_Usuarios_MedicoCoordenadorUsuarioId",
                        column: x => x.MedicoCoordenadorUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PcmsoItensMatriz",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PcmsoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FuncaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiscoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NomeExame = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PeriodicidadeEmMeses = table.Column<int>(type: "int", nullable: false),
                    ObrigatorioNoAdmissional = table.Column<bool>(type: "bit", nullable: false),
                    ObrigatorioNoDemissional = table.Column<bool>(type: "bit", nullable: false),
                    Observacoes = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_PcmsoItensMatriz", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PcmsoItensMatriz_Funcoes_FuncaoId",
                        column: x => x.FuncaoId,
                        principalTable: "Funcoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PcmsoItensMatriz_Pcmsos_PcmsoId",
                        column: x => x.PcmsoId,
                        principalTable: "Pcmsos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PcmsoItensMatriz_Riscos_RiscoId",
                        column: x => x.RiscoId,
                        principalTable: "Riscos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PcmsoRevisoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PcmsoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroRevisao = table.Column<int>(type: "int", nullable: false),
                    DataRevisao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ResponsavelUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_PcmsoRevisoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PcmsoRevisoes_Pcmsos_PcmsoId",
                        column: x => x.PcmsoId,
                        principalTable: "Pcmsos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PcmsoRevisoes_Usuarios_ResponsavelUsuarioId",
                        column: x => x.ResponsavelUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotenciaRegistros_Chave",
                table: "IdempotenciaRegistros",
                column: "Chave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PcmsoItensMatriz_FuncaoId",
                table: "PcmsoItensMatriz",
                column: "FuncaoId");

            migrationBuilder.CreateIndex(
                name: "IX_PcmsoItensMatriz_PcmsoId_FuncaoId",
                table: "PcmsoItensMatriz",
                columns: new[] { "PcmsoId", "FuncaoId" });

            migrationBuilder.CreateIndex(
                name: "IX_PcmsoItensMatriz_RiscoId",
                table: "PcmsoItensMatriz",
                column: "RiscoId");

            migrationBuilder.CreateIndex(
                name: "IX_PcmsoRevisoes_PcmsoId_NumeroRevisao",
                table: "PcmsoRevisoes",
                columns: new[] { "PcmsoId", "NumeroRevisao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PcmsoRevisoes_ResponsavelUsuarioId",
                table: "PcmsoRevisoes",
                column: "ResponsavelUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Pcmsos_MedicoCoordenadorUsuarioId",
                table: "Pcmsos",
                column: "MedicoCoordenadorUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Pcmsos_ObraId",
                table: "Pcmsos",
                column: "ObraId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotenciaRegistros");

            migrationBuilder.DropTable(
                name: "PcmsoItensMatriz");

            migrationBuilder.DropTable(
                name: "PcmsoRevisoes");

            migrationBuilder.DropTable(
                name: "Pcmsos");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "UsuariosPerfilObra");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "UsuariosPerfilObra",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Usuarios");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Usuarios",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "TrilhaAuditoria");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "TrilhaAuditoria",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Treinamentos");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Treinamentos",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Trabalhadores");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Trabalhadores",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "TagsIdentificacao");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "TagsIdentificacao",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Setores");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Setores",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "RiscoTrabalhadorExpostos");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "RiscoTrabalhadorExpostos",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Riscos");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Riscos",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "RequisitosLegais");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "RequisitosLegais",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PlanoAcaoItens");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PlanoAcaoItens",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Pgrs");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Pgrs",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PgrRevisoes");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PgrRevisoes",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PermissoesTrabalho");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PermissoesTrabalho",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Permissoes");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Permissoes",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PermissaoTrabalhoResponsaveis");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PermissaoTrabalhoResponsaveis",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PermissaoTrabalhoRequisitos");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PermissaoTrabalhoRequisitos",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PermissaoTrabalhoPerigos");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PermissaoTrabalhoPerigos",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PermissaoTrabalhoControles");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PermissaoTrabalhoControles",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Perigos");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Perigos",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PerfisAcessoPermissoes");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PerfisAcessoPermissoes",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PerfisAcesso");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PerfisAcesso",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Obras");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Obras",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "NaoConformidades");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "NaoConformidades",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "MatrizRiscoConfigs");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "MatrizRiscoConfigs",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "MatrizRiscoCelulas");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "MatrizRiscoCelulas",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Inspecoes");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Inspecoes",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "InspecaoItemRespostas");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "InspecaoItemRespostas",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Funcoes");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Funcoes",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Evidencias");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Evidencias",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Equipes");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Equipes",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "EntregasEpi");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "EntregasEpi",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DocumentosGestao");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DocumentosGestao",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DocumentoRevisoes");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DocumentoRevisoes",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DdsTelegramEnvios");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DdsTelegramEnvios",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DdsParticipantes");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DdsParticipantes",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DdsItensChecklist");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DdsItensChecklist",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DdsAtividades");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DdsAtividades",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Dds");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Dds",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "CursosTreinamento");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "CursosTreinamento",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ChecklistModelos");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ChecklistModelos",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ChecklistModeloItens");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ChecklistModeloItens",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "CatalogoEpis");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "CatalogoEpis",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Atividades");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Atividades",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Asos");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Asos",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AsoRestricoes");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AsoRestricoes",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AreasSst");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AreasSst",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Aprs");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Aprs",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AprResponsaveis");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AprResponsaveis",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AprEtapas");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AprEtapas",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AprEtapaRiscos");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AprEtapaRiscos",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AprAssinaturas");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AprAssinaturas",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Alertas");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Alertas",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AlertaHistoricoEnvios");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AlertaHistoricoEnvios",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AcoesPlano");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AcoesPlano",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Acidentes");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Acidentes",
                type: "varbinary(max)",
                nullable: true);
        }
    }
}
