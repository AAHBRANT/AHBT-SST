using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarAtendimentoIntegracao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IntegracaoAtendimentoId",
                table: "SessoesTreinamento",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FotoHash",
                table: "FotosEvidenciaSessaoTreinamento",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IntegracoesAtendimento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ResponsavelUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InstrutorTrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EtapaAtualOrdem = table.Column<int>(type: "int", nullable: true),
                    IniciadaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConcluidaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_IntegracoesAtendimento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegracoesAtendimento_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntegracoesAtendimento_Trabalhadores_InstrutorTrabalhadorId",
                        column: x => x.InstrutorTrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntegracoesAtendimento_Usuarios_ResponsavelUsuarioId",
                        column: x => x.ResponsavelUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntegracaoEtapas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntegracaoAtendimentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    CursoTreinamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SessaoTreinamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CargaMinimaHoras = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IniciadaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConcluidaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_IntegracaoEtapas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegracaoEtapas_CursosTreinamento_CursoTreinamentoId",
                        column: x => x.CursoTreinamentoId,
                        principalTable: "CursosTreinamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntegracaoEtapas_IntegracoesAtendimento_IntegracaoAtendimentoId",
                        column: x => x.IntegracaoAtendimentoId,
                        principalTable: "IntegracoesAtendimento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntegracaoEtapas_SessoesTreinamento_SessaoTreinamentoId",
                        column: x => x.SessaoTreinamentoId,
                        principalTable: "SessoesTreinamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParticipantesIntegracao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntegracaoAtendimentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanoIntegracaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FuncaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FuncaoNome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OrdemServicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_ParticipantesIntegracao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParticipantesIntegracao_IntegracoesAtendimento_IntegracaoAtendimentoId",
                        column: x => x.IntegracaoAtendimentoId,
                        principalTable: "IntegracoesAtendimento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParticipantesIntegracao_PlanosIntegracao_PlanoIntegracaoId",
                        column: x => x.PlanoIntegracaoId,
                        principalTable: "PlanosIntegracao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParticipantesIntegracao_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegistrosIntegracao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntegracaoAtendimentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RealizadaEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TecnicoUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TecnicoNome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FuncaoNome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
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
                    table.PrimaryKey("PK_RegistrosIntegracao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrosIntegracao_IntegracoesAtendimento_IntegracaoAtendimentoId",
                        column: x => x.IntegracaoAtendimentoId,
                        principalTable: "IntegracoesAtendimento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegistrosIntegracao_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PeriodosEtapaIntegracao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntegracaoEtapaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Data = table.Column<DateTime>(type: "date", nullable: false),
                    Inicio = table.Column<TimeSpan>(type: "time", nullable: false),
                    Fim = table.Column<TimeSpan>(type: "time", nullable: false),
                    Minutos = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_PeriodosEtapaIntegracao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeriodosEtapaIntegracao_IntegracaoEtapas_IntegracaoEtapaId",
                        column: x => x.IntegracaoEtapaId,
                        principalTable: "IntegracaoEtapas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessoesTreinamento_IntegracaoAtendimentoId",
                table: "SessoesTreinamento",
                column: "IntegracaoAtendimentoId",
                filter: "[IntegracaoAtendimentoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FotosEvidenciaSessaoTreinamento_FotoHash",
                table: "FotosEvidenciaSessaoTreinamento",
                column: "FotoHash",
                filter: "[FotoHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IntegracaoEtapas_CursoTreinamentoId",
                table: "IntegracaoEtapas",
                column: "CursoTreinamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegracaoEtapas_IntegracaoAtendimentoId_Ordem",
                table: "IntegracaoEtapas",
                columns: new[] { "IntegracaoAtendimentoId", "Ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegracaoEtapas_SessaoTreinamentoId",
                table: "IntegracaoEtapas",
                column: "SessaoTreinamentoId",
                filter: "[SessaoTreinamentoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IntegracoesAtendimento_InstrutorTrabalhadorId",
                table: "IntegracoesAtendimento",
                column: "InstrutorTrabalhadorId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegracoesAtendimento_Numero",
                table: "IntegracoesAtendimento",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegracoesAtendimento_ObraId_EmAndamento",
                table: "IntegracoesAtendimento",
                column: "ObraId",
                unique: true,
                filter: "[Ativo] = 1 AND [Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_IntegracoesAtendimento_ResponsavelUsuarioId",
                table: "IntegracoesAtendimento",
                column: "ResponsavelUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantesIntegracao_IntegracaoAtendimentoId_TrabalhadorId",
                table: "ParticipantesIntegracao",
                columns: new[] { "IntegracaoAtendimentoId", "TrabalhadorId" },
                unique: true,
                filter: "[Ativo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantesIntegracao_PlanoIntegracaoId",
                table: "ParticipantesIntegracao",
                column: "PlanoIntegracaoId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantesIntegracao_TrabalhadorId",
                table: "ParticipantesIntegracao",
                column: "TrabalhadorId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodosEtapaIntegracao_IntegracaoEtapaId",
                table: "PeriodosEtapaIntegracao",
                column: "IntegracaoEtapaId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosIntegracao_IntegracaoAtendimentoId_TrabalhadorId",
                table: "RegistrosIntegracao",
                columns: new[] { "IntegracaoAtendimentoId", "TrabalhadorId" },
                unique: true,
                filter: "[Ativo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosIntegracao_TrabalhadorId",
                table: "RegistrosIntegracao",
                column: "TrabalhadorId");

            migrationBuilder.AddForeignKey(
                name: "FK_SessoesTreinamento_IntegracoesAtendimento_IntegracaoAtendimentoId",
                table: "SessoesTreinamento",
                column: "IntegracaoAtendimentoId",
                principalTable: "IntegracoesAtendimento",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SessoesTreinamento_IntegracoesAtendimento_IntegracaoAtendimentoId",
                table: "SessoesTreinamento");

            migrationBuilder.DropTable(
                name: "ParticipantesIntegracao");

            migrationBuilder.DropTable(
                name: "PeriodosEtapaIntegracao");

            migrationBuilder.DropTable(
                name: "RegistrosIntegracao");

            migrationBuilder.DropTable(
                name: "IntegracaoEtapas");

            migrationBuilder.DropTable(
                name: "IntegracoesAtendimento");

            migrationBuilder.DropIndex(
                name: "IX_SessoesTreinamento_IntegracaoAtendimentoId",
                table: "SessoesTreinamento");

            migrationBuilder.DropIndex(
                name: "IX_FotosEvidenciaSessaoTreinamento_FotoHash",
                table: "FotosEvidenciaSessaoTreinamento");

            migrationBuilder.DropColumn(
                name: "IntegracaoAtendimentoId",
                table: "SessoesTreinamento");

            migrationBuilder.DropColumn(
                name: "FotoHash",
                table: "FotosEvidenciaSessaoTreinamento");
        }
    }
}
