using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class LeituraBiometricaAssinada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChavePublicaAssinatura",
                table: "DispositivosAgenteBiometrico",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ChavePublicaRegistradaEm",
                table: "DispositivosAgenteBiometrico",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DesafiosBiometria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DispositivoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiraEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_DesafiosBiometria", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DesafiosBiometria_ExpiraEm",
                table: "DesafiosBiometria",
                column: "ExpiraEm");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DesafiosBiometria");

            migrationBuilder.DropColumn(
                name: "ChavePublicaAssinatura",
                table: "DispositivosAgenteBiometrico");

            migrationBuilder.DropColumn(
                name: "ChavePublicaRegistradaEm",
                table: "DispositivosAgenteBiometrico");
        }
    }
}
