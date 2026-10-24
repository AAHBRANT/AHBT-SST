using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class HabilitarMetodosAssinaturaTodasObras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Decisão de 29/09/2026: toda obra aceita digital (1) e facial (2) — só dados, sem mudança de schema.
            migrationBuilder.Sql("UPDATE Obras SET MetodosAutenticacaoHabilitados = 3 WHERE MetodosAutenticacaoHabilitados <> 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
