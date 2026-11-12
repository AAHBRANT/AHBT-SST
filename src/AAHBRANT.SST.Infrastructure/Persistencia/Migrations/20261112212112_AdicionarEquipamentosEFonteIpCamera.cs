using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarEquipamentosEFonteIpCamera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Canal",
                table: "Cameras",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EquipamentoCameraId",
                table: "Cameras",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Fonte",
                table: "Cameras",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IntervaloEnvioSegundos",
                table: "Cameras",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<string>(
                name: "MensagemConexao",
                table: "Cameras",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SituacaoConexao",
                table: "Cameras",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Stream",
                table: "Cameras",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UltimoQuadroRecebidoEmUtc",
                table: "Cameras",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EquipamentosCamera",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgenteCameraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Host = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PortaRtsp = table.Column<int>(type: "int", nullable: false),
                    PortaOnvif = table.Column<int>(type: "int", nullable: false),
                    EnderecoMac = table.Column<string>(type: "nvarchar(17)", maxLength: 17, nullable: true),
                    Usuario = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SenhaCifrada = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: true),
                    Fabricante = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Modelo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UltimoIpDescobertoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_EquipamentosCamera", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipamentosCamera_AgentesCamera_AgenteCameraId",
                        column: x => x.AgenteCameraId,
                        principalTable: "AgentesCamera",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipamentosCamera_Obras_ObraId",
                        column: x => x.ObraId,
                        principalTable: "Obras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TestesConexaoCamera",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgenteCameraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipamentoCameraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CameraId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Canal = table.Column<int>(type: "int", nullable: false),
                    Stream = table.Column<int>(type: "int", nullable: false),
                    SolicitadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntregueAoAgenteEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConcluidoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Sucesso = table.Column<bool>(type: "bit", nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Detalhe = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Miniatura = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
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
                    table.PrimaryKey("PK_TestesConexaoCamera", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cameras_EquipamentoCameraId",
                table: "Cameras",
                column: "EquipamentoCameraId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipamentosCamera_AgenteCameraId",
                table: "EquipamentosCamera",
                column: "AgenteCameraId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipamentosCamera_ObraId_Nome",
                table: "EquipamentosCamera",
                columns: new[] { "ObraId", "Nome" },
                unique: true,
                filter: "[Ativo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TestesConexaoCamera_AgenteCameraId_SolicitadoEmUtc",
                table: "TestesConexaoCamera",
                columns: new[] { "AgenteCameraId", "SolicitadoEmUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_Cameras_EquipamentosCamera_EquipamentoCameraId",
                table: "Cameras",
                column: "EquipamentoCameraId",
                principalTable: "EquipamentosCamera",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cameras_EquipamentosCamera_EquipamentoCameraId",
                table: "Cameras");

            migrationBuilder.DropTable(
                name: "EquipamentosCamera");

            migrationBuilder.DropTable(
                name: "TestesConexaoCamera");

            migrationBuilder.DropIndex(
                name: "IX_Cameras_EquipamentoCameraId",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "Canal",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "EquipamentoCameraId",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "Fonte",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "IntervaloEnvioSegundos",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "MensagemConexao",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "SituacaoConexao",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "Stream",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "UltimoQuadroRecebidoEmUtc",
                table: "Cameras");
        }
    }
}
