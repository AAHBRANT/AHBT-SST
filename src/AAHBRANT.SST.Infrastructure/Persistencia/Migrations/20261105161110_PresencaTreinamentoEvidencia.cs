using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class PresencaTreinamentoEvidencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DispositivoAgenteId",
                table: "ParticipantesSessaoTreinamento",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenciaContentType",
                table: "ParticipantesSessaoTreinamento",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "EvidenciaConteudo",
                table: "ParticipantesSessaoTreinamento",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenciaHash",
                table: "ParticipantesSessaoTreinamento",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "ParticipantesSessaoTreinamento",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "ParticipantesSessaoTreinamento",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LocalizacaoStatus",
                table: "ParticipantesSessaoTreinamento",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "ParticipantesSessaoTreinamento",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PrecisaoMetros",
                table: "ParticipantesSessaoTreinamento",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "ParticipantesSessaoTreinamento",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidacaoGrupoId",
                table: "ParticipantesSessaoTreinamento",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidacaoModelo",
                table: "ParticipantesSessaoTreinamento",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidacaoRequisicaoId",
                table: "ParticipantesSessaoTreinamento",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DispositivoAgenteId",
                table: "ParticipantesSessaoTreinamento");

            migrationBuilder.DropColumn(
                name: "EvidenciaContentType",
                table: "ParticipantesSessaoTreinamento");

            migrationBuilder.DropColumn(
                name: "EvidenciaConteudo",
                table: "ParticipantesSessaoTreinamento");

            migrationBuilder.DropColumn(
                name: "EvidenciaHash",
                table: "ParticipantesSessaoTreinamento");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "ParticipantesSessaoTreinamento");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "ParticipantesSessaoTreinamento");

            migrationBuilder.DropColumn(
                name: "LocalizacaoStatus",
                table: "ParticipantesSessaoTreinamento");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "ParticipantesSessaoTreinamento");

            migrationBuilder.DropColumn(
                name: "PrecisaoMetros",
                table: "ParticipantesSessaoTreinamento");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "ParticipantesSessaoTreinamento");

            migrationBuilder.DropColumn(
                name: "ValidacaoGrupoId",
                table: "ParticipantesSessaoTreinamento");

            migrationBuilder.DropColumn(
                name: "ValidacaoModelo",
                table: "ParticipantesSessaoTreinamento");

            migrationBuilder.DropColumn(
                name: "ValidacaoRequisicaoId",
                table: "ParticipantesSessaoTreinamento");
        }
    }
}
