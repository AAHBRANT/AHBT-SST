using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

// Seeder idempotente (mesmo padrão do RegraAlertaSeeder) que publica o changelog do pop-up de
// novidades (ver NovidadeVersao) sem depender de alguém logar e cadastrar manualmente pela tela de
// Administração. Regra do usuário (18/09): toda vez que uma mudança visível ao usuário for
// deployada, adicionar uma entrada aqui ANTES do commit/push — o seeder aplica no start da
// aplicação (Program.cs, depois das migrations), então a novidade já existe assim que o deploy sobe
// em qualquer ambiente (dev/hml/produção), sem chamada HTTP autenticada.
//
// Checa por Versao (não por linha exata) — se a versão já existe, não insere de novo. Se algum dia
// for preciso corrigir uma novidade já publicada, isso é feito pela tela de Administração (edição
// manual), não editando uma entrada já semeada aqui.
public record NovidadeSeedItem(CategoriaNovidade Categoria, string Descricao, string? Antes, string? Agora);

public record NovidadeSeed(string Versao, string Titulo, DateTime DataPublicacao, NovidadeSeedItem[] Itens);

public static class NovidadesSeeder
{
    private static readonly NovidadeSeed[] Novidades =
    {
        new(
            Versao: "5.11.0",
            Titulo: "Novidade: pop-up de atualizações!",
            DataPublicacao: new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
            Itens: new[]
            {
                new NovidadeSeedItem(
                    CategoriaNovidade.Novidade,
                    "Agora você vê um resumo do que mudou a cada atualização do sistema",
                    "Não havia nenhum aviso sobre o que mudava quando o sistema era atualizado.",
                    "Um pop-up aparece automaticamente ao entrar, mostrando o que foi corrigido, melhorado ou adicionado — clique em cada item para ver o antes e depois."),
            }),
    };

    public static async Task ExecutarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();

        var versoesExistentes = await db.NovidadesVersao
            .IgnoreQueryFilters()
            .Select(n => n.Versao)
            .ToListAsync(ct);

        foreach (var novidade in Novidades)
        {
            if (versoesExistentes.Contains(novidade.Versao)) continue;

            var entidade = new NovidadeVersao
            {
                Titulo = novidade.Titulo,
                Versao = novidade.Versao,
                DataPublicacao = novidade.DataPublicacao,
            };

            var ordem = 0;
            foreach (var item in novidade.Itens)
            {
                entidade.Itens.Add(new NovidadeVersaoItem
                {
                    Categoria = item.Categoria,
                    Descricao = item.Descricao,
                    Antes = item.Antes,
                    Agora = item.Agora,
                    Ordem = ordem++,
                });
            }

            db.NovidadesVersao.Add(entidade);
        }

        await db.SaveChangesAsync(ct);
    }
}
