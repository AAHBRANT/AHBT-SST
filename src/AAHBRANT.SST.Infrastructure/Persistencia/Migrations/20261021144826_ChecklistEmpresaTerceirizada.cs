using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class ChecklistEmpresaTerceirizada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentosExigidosEmpresa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExigeValidade = table.Column<bool>(type: "bit", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_DocumentosExigidosEmpresa", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentosEmpresaTerceirizada",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentoExigidoEmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataValidade = table.Column<DateOnly>(type: "date", nullable: true),
                    NomeArquivo = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TamanhoBytes = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_DocumentosEmpresaTerceirizada", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentosEmpresaTerceirizada_DocumentosExigidosEmpresa_DocumentoExigidoEmpresaId",
                        column: x => x.DocumentoExigidoEmpresaId,
                        principalTable: "DocumentosExigidosEmpresa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentosEmpresaTerceirizada_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArquivosDocumentoEmpresaTerceirizada",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentoEmpresaTerceirizadaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Conteudo = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
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
                    table.PrimaryKey("PK_ArquivosDocumentoEmpresaTerceirizada", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArquivosDocumentoEmpresaTerceirizada_DocumentosEmpresaTerceirizada_DocumentoEmpresaTerceirizadaId",
                        column: x => x.DocumentoEmpresaTerceirizadaId,
                        principalTable: "DocumentosEmpresaTerceirizada",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArquivosDocumentoEmpresaTerceirizada_DocumentoEmpresaTerceirizadaId",
                table: "ArquivosDocumentoEmpresaTerceirizada",
                column: "DocumentoEmpresaTerceirizadaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosEmpresaTerceirizada_DocumentoExigidoEmpresaId",
                table: "DocumentosEmpresaTerceirizada",
                column: "DocumentoExigidoEmpresaId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosEmpresaTerceirizada_EmpresaId_DocumentoExigidoEmpresaId",
                table: "DocumentosEmpresaTerceirizada",
                columns: new[] { "EmpresaId", "DocumentoExigidoEmpresaId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArquivosDocumentoEmpresaTerceirizada");

            migrationBuilder.DropTable(
                name: "DocumentosEmpresaTerceirizada");

            migrationBuilder.DropTable(
                name: "DocumentosExigidosEmpresa");
        }
    }
}
