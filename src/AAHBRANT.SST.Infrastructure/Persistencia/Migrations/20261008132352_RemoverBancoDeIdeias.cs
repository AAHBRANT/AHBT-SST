using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class RemoverBancoDeIdeias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemandasDesenvolvimento");

            migrationBuilder.DropTable(
                name: "IdeiaAnexos");

            migrationBuilder.DropTable(
                name: "IdeiaComentarios");

            migrationBuilder.DropTable(
                name: "IdeiaHistoricos");

            migrationBuilder.DropTable(
                name: "IdeiaRequisitos");

            migrationBuilder.DropTable(
                name: "Ideias");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ideias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdeiaPrincipalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IdeiaSemelhanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RegistradoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResponsavelAnaliseUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResponsavelDesenvolvimentoUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    BeneficioEsperado = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Canal = table.Column<int>(type: "int", nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Complexidade = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAprovacaoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataConclusaoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataImplantacaoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataInicioUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecididoPorNome = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Decisao = table.Column<int>(type: "int", nullable: true),
                    Dependencias = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Esforco = table.Column<int>(type: "int", nullable: true),
                    EsforcoEstimado = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    EstruturadoPor = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Impacto = table.Column<int>(type: "int", nullable: true),
                    InformacoesFaltantes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IntegracoesNecessarias = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Justificativa = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MensagemOriginal = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Modulo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    NecessidadeIa = table.Column<bool>(type: "bit", nullable: true),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Objetivo = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Observacoes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    PerguntaMensagemId = table.Column<long>(type: "bigint", nullable: true),
                    PerguntaPendente = table.Column<int>(type: "int", nullable: false),
                    Pontuacao = table.Column<int>(type: "int", nullable: true),
                    PossiveisImpactos = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Prioridade = table.Column<int>(type: "int", nullable: true),
                    ProblemaOportunidade = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RegistradoPorNome = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    ResponsavelAnaliseNome = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    ResponsavelDesenvolvimentoNome = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    SolucaoSugerida = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Submodulo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    TelegramChatId = table.Column<long>(type: "bigint", nullable: true),
                    TelegramMensagemId = table.Column<long>(type: "bigint", nullable: true),
                    TelegramUsuarioId = table.Column<long>(type: "bigint", nullable: true),
                    TelegramUsuarioNome = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Titulo = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Urgencia = table.Column<int>(type: "int", nullable: true),
                    ValorNegocio = table.Column<int>(type: "int", nullable: true),
                    ViabilidadeOperacional = table.Column<int>(type: "int", nullable: false),
                    ViabilidadeTecnica = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ideias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ideias_Ideias_IdeiaPrincipalId",
                        column: x => x.IdeiaPrincipalId,
                        principalTable: "Ideias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ideias_Ideias_IdeiaSemelhanteId",
                        column: x => x.IdeiaSemelhanteId,
                        principalTable: "Ideias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ideias_Usuarios_RegistradoPorUsuarioId",
                        column: x => x.RegistradoPorUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ideias_Usuarios_ResponsavelAnaliseUsuarioId",
                        column: x => x.ResponsavelAnaliseUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ideias_Usuarios_ResponsavelDesenvolvimentoUsuarioId",
                        column: x => x.ResponsavelDesenvolvimentoUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IdeiaAnexos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdeiaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Conteudo = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EnviadoPorNome = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    NomeArquivo = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Tamanho = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdeiaAnexos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdeiaAnexos_Ideias_IdeiaId",
                        column: x => x.IdeiaId,
                        principalTable: "Ideias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IdeiaComentarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdeiaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    AutorNome = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    AutorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Texto = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdeiaComentarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdeiaComentarios_Ideias_IdeiaId",
                        column: x => x.IdeiaId,
                        principalTable: "Ideias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IdeiaHistoricos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdeiaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    AutorNome = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    AutorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OcorridoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdeiaHistoricos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdeiaHistoricos_Ideias_IdeiaId",
                        column: x => x.IdeiaId,
                        principalTable: "Ideias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IdeiaRequisitos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdeiaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AprovadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AprovadoPorNome = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CriteriosAceite = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdeiaRequisitos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdeiaRequisitos_Ideias_IdeiaId",
                        column: x => x.IdeiaId,
                        principalTable: "Ideias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DemandasDesenvolvimento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdeiaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequisitoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ConcluidaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    FuncionalidadeEntregue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    ResponsavelNome = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    ResponsavelUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandasDesenvolvimento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DemandasDesenvolvimento_IdeiaRequisitos_RequisitoId",
                        column: x => x.RequisitoId,
                        principalTable: "IdeiaRequisitos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DemandasDesenvolvimento_Ideias_IdeiaId",
                        column: x => x.IdeiaId,
                        principalTable: "Ideias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DemandasDesenvolvimento_Codigo",
                table: "DemandasDesenvolvimento",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DemandasDesenvolvimento_IdeiaId",
                table: "DemandasDesenvolvimento",
                column: "IdeiaId");

            migrationBuilder.CreateIndex(
                name: "IX_DemandasDesenvolvimento_RequisitoId",
                table: "DemandasDesenvolvimento",
                column: "RequisitoId");

            migrationBuilder.CreateIndex(
                name: "IX_IdeiaAnexos_IdeiaId",
                table: "IdeiaAnexos",
                column: "IdeiaId");

            migrationBuilder.CreateIndex(
                name: "IX_IdeiaComentarios_IdeiaId",
                table: "IdeiaComentarios",
                column: "IdeiaId");

            migrationBuilder.CreateIndex(
                name: "IX_IdeiaHistoricos_IdeiaId",
                table: "IdeiaHistoricos",
                column: "IdeiaId");

            migrationBuilder.CreateIndex(
                name: "IX_IdeiaRequisitos_IdeiaId",
                table: "IdeiaRequisitos",
                column: "IdeiaId");

            migrationBuilder.CreateIndex(
                name: "IX_Ideias_Codigo",
                table: "Ideias",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ideias_IdeiaPrincipalId",
                table: "Ideias",
                column: "IdeiaPrincipalId");

            migrationBuilder.CreateIndex(
                name: "IX_Ideias_IdeiaSemelhanteId",
                table: "Ideias",
                column: "IdeiaSemelhanteId");

            migrationBuilder.CreateIndex(
                name: "IX_Ideias_Modulo",
                table: "Ideias",
                column: "Modulo");

            migrationBuilder.CreateIndex(
                name: "IX_Ideias_RegistradoPorUsuarioId",
                table: "Ideias",
                column: "RegistradoPorUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Ideias_ResponsavelAnaliseUsuarioId",
                table: "Ideias",
                column: "ResponsavelAnaliseUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Ideias_ResponsavelDesenvolvimentoUsuarioId",
                table: "Ideias",
                column: "ResponsavelDesenvolvimentoUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Ideias_Status",
                table: "Ideias",
                column: "Status");
        }
    }
}
