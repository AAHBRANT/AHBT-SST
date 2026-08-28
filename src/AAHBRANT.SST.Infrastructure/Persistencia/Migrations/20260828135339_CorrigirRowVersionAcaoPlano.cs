using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CorrigirRowVersionAcaoPlano : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mesmo padrão de CorrigirRowVersionDocumentoGestao: guardado com IF + SQL dinâmico
            // (EXEC), pois o SQL Server valida ALTER COLUMN...rowversion em tempo de compilação do
            // batch, ignorando o IF em tempo de execução (erro 4927 se a coluna já for rowversion).
            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1 FROM sys.columns c
    JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID(N'[AcoesPlano]') AND c.name = N'RowVersion' AND t.name = 'timestamp'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AcoesPlano]') AND [c].[name] = N'RowVersion');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [AcoesPlano] DROP CONSTRAINT [' + @var0 + '];');
    EXEC(N'ALTER TABLE [AcoesPlano] ALTER COLUMN [RowVersion] rowversion NULL;');
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "AcoesPlano",
                type: "varbinary(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true,
                oldNullable: true);
        }
    }
}
