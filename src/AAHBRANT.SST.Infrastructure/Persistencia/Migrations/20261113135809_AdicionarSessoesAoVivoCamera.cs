using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarSessoesAoVivoCamera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SessoesAoVivoCamera",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CameraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgenteCameraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolicitadaPorUsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SolicitadaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UltimaVisualizacaoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LimiteEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IniciadaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EncerradaEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoEncerramento = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CodigoFalha = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Fps = table.Column<int>(type: "int", nullable: false),
                    UltimoQuadro = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    UltimoQuadroSequencia = table.Column<long>(type: "bigint", nullable: false),
                    UltimoQuadroCapturadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimoQuadroRecebidoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    QuadrosRecebidos = table.Column<long>(type: "bigint", nullable: false),
                    BytesRecebidos = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_SessoesAoVivoCamera", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessoesAoVivoCamera_AgenteCameraId_EncerradaEmUtc",
                table: "SessoesAoVivoCamera",
                columns: new[] { "AgenteCameraId", "EncerradaEmUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SessoesAoVivoCamera_CameraId_Aberta",
                table: "SessoesAoVivoCamera",
                column: "CameraId",
                unique: true,
                filter: "[EncerradaEmUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SessoesAoVivoCamera_CameraId_EncerradaEmUtc",
                table: "SessoesAoVivoCamera",
                columns: new[] { "CameraId", "EncerradaEmUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SessoesAoVivoCamera");
        }
    }
}
