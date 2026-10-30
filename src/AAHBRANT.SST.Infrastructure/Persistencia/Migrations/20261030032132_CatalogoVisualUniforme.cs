using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CatalogoVisualUniforme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferenciaVisual",
                table: "CatalogoUniformes",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            // Autorização do usuário em 30/10/2026: os dois itens legados são laranja.
            // Atualiza as mesmas linhas. Nenhum ID, saldo, tamanho ou entrega é recriado.
            migrationBuilder.Sql(@"UPDATE [CatalogoUniformes]
                SET [Nome] = REPLACE(UPPER(LTRIM(RTRIM([Nome]))), N'BROM', N'BRIM')");
            migrationBuilder.Sql(@"UPDATE [CatalogoUniformes] SET [Nome] = N'CALÇA BRIM' WHERE [Nome] = N'CALCA BRIM'");
            migrationBuilder.Sql(@"UPDATE [CatalogoUniformes]
                SET [ReferenciaVisual] = CASE
                    WHEN [Nome] IN (N'CALÇA BRIM', N'CALCA BRIM') THEN N'calca-brim-laranja'
                    WHEN [Nome] = N'CAMISA BRIM MANGA LONGA' THEN N'camisa-brim-laranja'
                    ELSE [ReferenciaVisual] END
                WHERE [ReferenciaVisual] IS NULL AND [Nome] IN
                    (N'CALÇA BRIM', N'CALCA BRIM', N'CAMISA BRIM MANGA LONGA')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReferenciaVisual",
                table: "CatalogoUniformes");
        }
    }
}
