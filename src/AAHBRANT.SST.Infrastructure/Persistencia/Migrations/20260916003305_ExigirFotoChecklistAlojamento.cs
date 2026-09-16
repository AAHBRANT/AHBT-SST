using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    // Migração de dados (sem mudança de schema): atualiza ambientes já seedados antes desta versão
    // do ChecklistAlojamentoSeeder.cs (Task 6, 2026-09-15) — ambientes novos já nascem com
    // ExigeFotografia = true direto pelo seeder. Nome da tabela (ChecklistModeloItens) confirmado
    // em SstDbContextModelSnapshot.cs; TipoInspecao.Alojamento = 14 confirmado em Enums.cs.
    public partial class ExigirFotoChecklistAlojamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE cmi
                SET cmi.ExigeFotografia = 1
                FROM ChecklistModeloItens cmi
                INNER JOIN ChecklistModelos cm ON cm.Id = cmi.ChecklistModeloId
                WHERE cm.TipoInspecao = 14;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE cmi
                SET cmi.ExigeFotografia = 0
                FROM ChecklistModeloItens cmi
                INNER JOIN ChecklistModelos cm ON cm.Id = cmi.ChecklistModeloId
                WHERE cm.TipoInspecao = 14;
            ");
        }
    }
}
