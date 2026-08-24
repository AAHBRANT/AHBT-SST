using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarPermissaoTrabalho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PermissoesTrabalho",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AtividadeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Local = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EquipeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Data = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HorarioInicio = table.Column<TimeSpan>(type: "time", nullable: true),
                    HorarioFim = table.Column<TimeSpan>(type: "time", nullable: true),
                    Validade = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AutorizadoPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAutorizacao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EncerradaPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataEncerramento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ObservacoesEncerramento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_PermissoesTrabalho", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermissoesTrabalho_Atividades_AtividadeId",
                        column: x => x.AtividadeId,
                        principalTable: "Atividades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PermissoesTrabalho_Equipes_EquipeId",
                        column: x => x.EquipeId,
                        principalTable: "Equipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PermissoesTrabalho_Usuarios_AutorizadoPorUsuarioId",
                        column: x => x.AutorizadoPorUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PermissoesTrabalho_Usuarios_EncerradaPorUsuarioId",
                        column: x => x.EncerradaPorUsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PermissaoTrabalhoControles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissaoTrabalhoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
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
                    PermissaoTrabalhoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PerigoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    Descricao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Atendido = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_PermissaoTrabalhoRequisitos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermissaoTrabalhoRequisitos_PermissoesTrabalho_PermissaoTrabalhoId",
                        column: x => x.PermissaoTrabalhoId,
                        principalTable: "PermissoesTrabalho",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PermissaoTrabalhoResponsaveis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissaoTrabalhoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_PermissaoTrabalhoResponsaveis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermissaoTrabalhoResponsaveis_PermissoesTrabalho_PermissaoTrabalhoId",
                        column: x => x.PermissaoTrabalhoId,
                        principalTable: "PermissoesTrabalho",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PermissaoTrabalhoResponsaveis_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.CreateIndex(
                name: "IX_PermissaoTrabalhoResponsaveis_PermissaoTrabalhoId_TrabalhadorId",
                table: "PermissaoTrabalhoResponsaveis",
                columns: new[] { "PermissaoTrabalhoId", "TrabalhadorId" });

            migrationBuilder.CreateIndex(
                name: "IX_PermissaoTrabalhoResponsaveis_TrabalhadorId",
                table: "PermissaoTrabalhoResponsaveis",
                column: "TrabalhadorId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissoesTrabalho_AtividadeId",
                table: "PermissoesTrabalho",
                column: "AtividadeId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissoesTrabalho_AutorizadoPorUsuarioId",
                table: "PermissoesTrabalho",
                column: "AutorizadoPorUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissoesTrabalho_EncerradaPorUsuarioId",
                table: "PermissoesTrabalho",
                column: "EncerradaPorUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissoesTrabalho_EquipeId",
                table: "PermissoesTrabalho",
                column: "EquipeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PermissaoTrabalhoControles");

            migrationBuilder.DropTable(
                name: "PermissaoTrabalhoPerigos");

            migrationBuilder.DropTable(
                name: "PermissaoTrabalhoRequisitos");

            migrationBuilder.DropTable(
                name: "PermissaoTrabalhoResponsaveis");

            migrationBuilder.DropTable(
                name: "PermissoesTrabalho");
        }
    }
}
