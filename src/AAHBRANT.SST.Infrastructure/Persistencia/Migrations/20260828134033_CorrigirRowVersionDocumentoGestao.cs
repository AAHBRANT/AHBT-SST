using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CorrigirRowVersionDocumentoGestao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Guardado com IF + SQL dinâmico (EXEC): em bancos onde a coluna já foi corrigida
            // manualmente/por outro branch que compartilha este banco de dev (AAHBRANT.SST.Dev),
            // a coluna já é "rowversion". O SQL Server valida um ALTER COLUMN...rowversion em
            // tempo de compilação do batch inteiro (erro 4927 "Cannot alter column to be data
            // type timestamp"), então um IF comum não evita o erro — é preciso EXEC(N'...') para
            // adiar a validação para o runtime, quando o IF já decidiu não executar o ALTER.
            // Em um banco criado do zero (hml/prod), a coluna ainda está "varbinary(max)" e o
            // ALTER roda normalmente dentro do EXEC.
            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1 FROM sys.columns c
    JOIN sys.types t ON c.user_type_id = t.user_type_id
    WHERE c.object_id = OBJECT_ID(N'[DocumentosGestao]') AND c.name = N'RowVersion' AND t.name = 'timestamp'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DocumentosGestao]') AND [c].[name] = N'RowVersion');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [DocumentosGestao] DROP CONSTRAINT [' + @var0 + '];');
    EXEC(N'ALTER TABLE [DocumentosGestao] ALTER COLUMN [RowVersion] rowversion NULL;');
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "DocumentosGestao",
                type: "varbinary(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true,
                oldNullable: true);
        }
    }
}
