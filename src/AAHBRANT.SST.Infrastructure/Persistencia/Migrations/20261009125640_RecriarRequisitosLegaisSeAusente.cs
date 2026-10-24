using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    // Reparo de schema (09/10/2026): em hml a tabela RequisitosLegais não existe, embora
    // ModuloRequisitosLegaisFase1 conste como aplicada. Causa: RemoverMatrizLegalEGestaoDocumental
    // (timestamp 28/08, vinda de outra branch) foi aplicada DEPOIS da Fase1 (29/08) e, por ser
    // idempotente, derrubou a FK de RequisitoLegalCriterios e a tabela RequisitosLegais nova.
    // Sem mudança de modelo: só recria o que faltar. Em bancos íntegros (dev, testes) não faz nada.
    /// <inheritdoc />
    public partial class RecriarRequisitosLegaisSeAusente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[RequisitosLegais]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [RequisitosLegais] (
                        [Id] uniqueidentifier NOT NULL,
                        [Norma] nvarchar(60) NOT NULL,
                        [Artigo] nvarchar(60) NULL,
                        [Titulo] nvarchar(300) NOT NULL,
                        [Descricao] nvarchar(2000) NOT NULL,
                        [Categoria] int NOT NULL,
                        [Status] int NOT NULL,
                        [Fonte] nvarchar(500) NULL,
                        [CreatedAtUtc] datetime2 NOT NULL,
                        [CreatedBy] uniqueidentifier NULL,
                        [UpdatedAtUtc] datetime2 NULL,
                        [UpdatedBy] uniqueidentifier NULL,
                        [Origem] int NOT NULL,
                        [Ativo] bit NOT NULL,
                        [RowVersion] rowversion NULL,
                        CONSTRAINT [PK_RequisitosLegais] PRIMARY KEY ([Id])
                    );
                    CREATE INDEX [IX_RequisitosLegais_Categoria] ON [RequisitosLegais] ([Categoria]);
                END");

            // A FK só volta se não houver critério órfão (apontando para requisito inexistente) —
            // nada é apagado aqui; se houver órfão, a FK fica de fora e o resto segue funcionando.
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[RequisitoLegalCriterios]', N'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_RequisitoLegalCriterios_RequisitosLegais_RequisitoLegalId')
                   AND NOT EXISTS (
                       SELECT 1 FROM [RequisitoLegalCriterios] c
                       WHERE NOT EXISTS (SELECT 1 FROM [RequisitosLegais] r WHERE r.[Id] = c.[RequisitoLegalId]))
                BEGIN
                    ALTER TABLE [RequisitoLegalCriterios] ADD CONSTRAINT [FK_RequisitoLegalCriterios_RequisitosLegais_RequisitoLegalId]
                        FOREIGN KEY ([RequisitoLegalId]) REFERENCES [RequisitosLegais] ([Id]) ON DELETE CASCADE;
                END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reparo sem volta: desfazer recriaria o defeito.
        }
    }
}
