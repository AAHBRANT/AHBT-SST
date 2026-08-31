using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCipa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DimensionamentosCipa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cnae = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GrauRisco = table.Column<int>(type: "int", nullable: false),
                    NumeroFuncionarios = table.Column<int>(type: "int", nullable: false),
                    NumeroTitulares = table.Column<int>(type: "int", nullable: false),
                    NumeroSuplentes = table.Column<int>(type: "int", nullable: false),
                    DataCalculo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Observacoes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_DimensionamentosCipa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DimensionamentosCipa_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcessosEleitoraisCipa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DataConvocacao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataInicioInscricoes = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataFimInscricoes = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataVotacao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataApuracao = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ProcessosEleitoraisCipa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessosEleitoraisCipa_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CandidatosCipa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessoEleitoralId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataInscricao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    MotivoIndeferimento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VotosRecebidos = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_CandidatosCipa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidatosCipa_ProcessosEleitoraisCipa_ProcessoEleitoralId",
                        column: x => x.ProcessoEleitoralId,
                        principalTable: "ProcessosEleitoraisCipa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CandidatosCipa_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MembrosCipa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrigemMembro = table.Column<int>(type: "int", nullable: false),
                    Cargo = table.Column<int>(type: "int", nullable: false),
                    DataInicioMandato = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataFimMandato = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessoEleitoralId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CandidatoCipaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_MembrosCipa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MembrosCipa_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MembrosCipa_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MembrosCipa_ProcessosEleitoraisCipa_ProcessoEleitoralId",
                        column: x => x.ProcessoEleitoralId,
                        principalTable: "ProcessosEleitoraisCipa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MembrosCipa_CandidatosCipa_CandidatoCipaId",
                        column: x => x.CandidatoCipaId,
                        principalTable: "CandidatosCipa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TreinamentosCipa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MembroCipaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CargaHoraria = table.Column<int>(type: "int", nullable: false),
                    ConteudoProgramatico = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DataRealizacao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataValidade = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InstituicaoInstrutor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CertificadoConteudo = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    CertificadoContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ListaPresencaConteudo = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    ListaPresencaContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_TreinamentosCipa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TreinamentosCipa_MembrosCipa_MembroCipaId",
                        column: x => x.MembroCipaId,
                        principalTable: "MembrosCipa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReunioesCipa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    DataReuniao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Pauta = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Deliberacoes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_ReunioesCipa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReunioesCipa_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParticipantesReuniaoCipa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReuniaoCipaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Presente = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_ParticipantesReuniaoCipa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParticipantesReuniaoCipa_ReunioesCipa_ReuniaoCipaId",
                        column: x => x.ReuniaoCipaId,
                        principalTable: "ReunioesCipa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParticipantesReuniaoCipa_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InspecoesCipa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MembroCipaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Data = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Local = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RiscoIdentificado = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    GrauRisco = table.Column<int>(type: "int", nullable: true),
                    NaoConformidadeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_InspecoesCipa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspecoesCipa_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspecoesCipa_MembrosCipa_MembroCipaId",
                        column: x => x.MembroCipaId,
                        principalTable: "MembrosCipa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InspecoesCipa_NaoConformidades_NaoConformidadeId",
                        column: x => x.NaoConformidadeId,
                        principalTable: "NaoConformidades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventosSipat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnoReferencia = table.Column<int>(type: "int", nullable: false),
                    DataInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataFim = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Tema = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Programacao = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_EventosSipat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventosSipat_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AtividadesSipat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventoSipatId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Data = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Horario = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TemaPalestra = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Palestrante = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_AtividadesSipat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AtividadesSipat_EventosSipat_EventoSipatId",
                        column: x => x.EventoSipatId,
                        principalTable: "EventosSipat",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(name: "IX_DimensionamentosCipa_ObraId", table: "DimensionamentosCipa", column: "ObraId");

            migrationBuilder.CreateIndex(name: "IX_ProcessosEleitoraisCipa_ObraId", table: "ProcessosEleitoraisCipa", column: "ObraId");

            migrationBuilder.CreateIndex(name: "IX_CandidatosCipa_ProcessoEleitoralId", table: "CandidatosCipa", column: "ProcessoEleitoralId");
            migrationBuilder.CreateIndex(name: "IX_CandidatosCipa_ProcessoEleitoralId_TrabalhadorId", table: "CandidatosCipa", columns: new[] { "ProcessoEleitoralId", "TrabalhadorId" });
            migrationBuilder.CreateIndex(name: "IX_CandidatosCipa_TrabalhadorId", table: "CandidatosCipa", column: "TrabalhadorId");

            migrationBuilder.CreateIndex(name: "IX_MembrosCipa_ObraId", table: "MembrosCipa", column: "ObraId");
            migrationBuilder.CreateIndex(name: "IX_MembrosCipa_TrabalhadorId", table: "MembrosCipa", column: "TrabalhadorId");
            migrationBuilder.CreateIndex(name: "IX_MembrosCipa_ProcessoEleitoralId", table: "MembrosCipa", column: "ProcessoEleitoralId");
            migrationBuilder.CreateIndex(name: "IX_MembrosCipa_CandidatoCipaId", table: "MembrosCipa", column: "CandidatoCipaId");

            migrationBuilder.CreateIndex(name: "IX_TreinamentosCipa_MembroCipaId", table: "TreinamentosCipa", column: "MembroCipaId");

            migrationBuilder.CreateIndex(name: "IX_ReunioesCipa_ObraId", table: "ReunioesCipa", column: "ObraId");

            migrationBuilder.CreateIndex(name: "IX_ParticipantesReuniaoCipa_ReuniaoCipaId_TrabalhadorId", table: "ParticipantesReuniaoCipa", columns: new[] { "ReuniaoCipaId", "TrabalhadorId" });
            migrationBuilder.CreateIndex(name: "IX_ParticipantesReuniaoCipa_TrabalhadorId", table: "ParticipantesReuniaoCipa", column: "TrabalhadorId");

            migrationBuilder.CreateIndex(name: "IX_InspecoesCipa_ObraId", table: "InspecoesCipa", column: "ObraId");
            migrationBuilder.CreateIndex(name: "IX_InspecoesCipa_MembroCipaId", table: "InspecoesCipa", column: "MembroCipaId");
            migrationBuilder.CreateIndex(name: "IX_InspecoesCipa_NaoConformidadeId", table: "InspecoesCipa", column: "NaoConformidadeId");

            migrationBuilder.CreateIndex(name: "IX_EventosSipat_ObraId", table: "EventosSipat", column: "ObraId");

            migrationBuilder.CreateIndex(name: "IX_AtividadesSipat_EventoSipatId", table: "AtividadesSipat", column: "EventoSipatId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AtividadesSipat");
            migrationBuilder.DropTable(name: "EventosSipat");
            migrationBuilder.DropTable(name: "InspecoesCipa");
            migrationBuilder.DropTable(name: "ParticipantesReuniaoCipa");
            migrationBuilder.DropTable(name: "ReunioesCipa");
            migrationBuilder.DropTable(name: "TreinamentosCipa");
            migrationBuilder.DropTable(name: "MembrosCipa");
            migrationBuilder.DropTable(name: "CandidatosCipa");
            migrationBuilder.DropTable(name: "ProcessosEleitoraisCipa");
            migrationBuilder.DropTable(name: "DimensionamentosCipa");
        }
    }
}
