using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class TemasSimultaneosDds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AtividadeNome",
                table: "DdsAtividades",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Consequencia",
                table: "DdsAtividades",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ControlesAdicionais",
                table: "DdsAtividades",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ControlesExistentes",
                table: "DdsAtividades",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PerigoDescricao",
                table: "DdsAtividades",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PerigoNome",
                table: "DdsAtividades",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemaLivreDescricao",
                table: "Dds",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemaLivreNome",
                table: "Dds",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            // Backfill para registros de Dds/DdsAtividade criados ANTES desta migração (esta branch já
            // está em uso em hml, com registros reais). Sem isto, todo DDS pré-existente ficaria com as
            // colunas novas NULL e passaria a mostrar "Nenhum risco cadastrado para esta atividade —
            // revisar Matriz de Riscos" para toda atividade, mesmo quando ela tem risco cadastrado hoje —
            // e o texto do antigo TopicoPrincipal seria perdido sem deixar rastro, já que a coluna é
            // removida logo abaixo. É um best-effort usando os dados de Risco ATUAIS como proxy (não é o
            // snapshot verdadeiro do momento da criação, que é genuinamente irrecuperável) — ainda assim
            // estritamente melhor do que exibir informação falsa, e o texto do tema antigo é preservado
            // literalmente em TemaLivreDescricao de qualquer forma.
            migrationBuilder.Sql(@"
                UPDATE da
                SET da.AtividadeNome = a.Nome,
                    da.PerigoNome = p.Nome,
                    da.PerigoDescricao = p.Descricao,
                    da.Consequencia = r.Consequencia,
                    da.ControlesExistentes = r.ControlesExistentes,
                    da.ControlesAdicionais = r.ControlesAdicionais
                FROM DdsAtividades da
                INNER JOIN Atividades a ON a.Id = da.AtividadeId
                OUTER APPLY (
                    SELECT TOP 1 r2.Consequencia, r2.ControlesExistentes, r2.ControlesAdicionais, r2.PerigoId
                    FROM Riscos r2
                    WHERE r2.AtividadeId = da.AtividadeId AND r2.Ativo = 1
                    ORDER BY r2.NivelRisco DESC
                ) r
                LEFT JOIN Perigos p ON p.Id = r.PerigoId
                WHERE da.PerigoNome IS NULL AND da.AtividadeNome IS NULL;
            ");

            migrationBuilder.Sql(@"
                UPDATE Dds
                SET TemaLivreNome = N'Tema registrado antes da reformulação de 01/09/2026',
                    TemaLivreDescricao = TopicoPrincipal
                WHERE TopicoPrincipal IS NOT NULL AND TopicoPrincipal <> N'' AND TemaLivreNome IS NULL;
            ");

            migrationBuilder.DropColumn(
                name: "OrigemTema",
                table: "Dds");

            migrationBuilder.DropColumn(
                name: "TopicoPrincipal",
                table: "Dds");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AtividadeNome",
                table: "DdsAtividades");

            migrationBuilder.DropColumn(
                name: "Consequencia",
                table: "DdsAtividades");

            migrationBuilder.DropColumn(
                name: "ControlesAdicionais",
                table: "DdsAtividades");

            migrationBuilder.DropColumn(
                name: "ControlesExistentes",
                table: "DdsAtividades");

            migrationBuilder.DropColumn(
                name: "PerigoDescricao",
                table: "DdsAtividades");

            migrationBuilder.DropColumn(
                name: "PerigoNome",
                table: "DdsAtividades");

            migrationBuilder.DropColumn(
                name: "TemaLivreDescricao",
                table: "Dds");

            migrationBuilder.DropColumn(
                name: "TemaLivreNome",
                table: "Dds");

            migrationBuilder.AddColumn<int>(
                name: "OrigemTema",
                table: "Dds",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TopicoPrincipal",
                table: "Dds",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }
    }
}
