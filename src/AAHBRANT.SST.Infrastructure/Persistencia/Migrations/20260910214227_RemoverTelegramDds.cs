using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class RemoverTelegramDds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DdsTelegramEnvios");

            migrationBuilder.DropColumn(
                name: "TelegramChatId",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "TelegramCodigoVinculo",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "TelegramVinculadoEm",
                table: "Trabalhadores");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TelegramChatId",
                table: "Trabalhadores",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelegramCodigoVinculo",
                table: "Trabalhadores",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TelegramVinculadoEm",
                table: "Trabalhadores",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DdsTelegramEnvios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DdsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrabalhadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    ConfirmadoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EnviadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MessageId = table.Column<int>(type: "int", nullable: true),
                    Origem = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DdsTelegramEnvios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DdsTelegramEnvios_Dds_DdsId",
                        column: x => x.DdsId,
                        principalTable: "Dds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DdsTelegramEnvios_Trabalhadores_TrabalhadorId",
                        column: x => x.TrabalhadorId,
                        principalTable: "Trabalhadores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DdsTelegramEnvios_DdsId_TrabalhadorId",
                table: "DdsTelegramEnvios",
                columns: new[] { "DdsId", "TrabalhadorId" });

            migrationBuilder.CreateIndex(
                name: "IX_DdsTelegramEnvios_TrabalhadorId",
                table: "DdsTelegramEnvios",
                column: "TrabalhadorId");
        }
    }
}
