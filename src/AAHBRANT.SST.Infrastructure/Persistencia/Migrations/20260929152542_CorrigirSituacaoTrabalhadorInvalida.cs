using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CorrigirSituacaoTrabalhadorInvalida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A migration 20260909151354 criou Trabalhadores.Situacao com valor padrão 0, mas o enum
            // SituacaoTrabalhador começa em 1 (Ativo). Todo trabalhador anterior àquela data (e o mock) ficou
            // com 0, um valor que não existe — e o DDS filtra por Situacao == Ativo, então esses funcionários
            // nunca apareciam na lista. 0 vira Ativo; desligamento continua valendo por DataDemissao/G-RH.
            migrationBuilder.Sql("UPDATE Trabalhadores SET Situacao = 1 WHERE Situacao = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
