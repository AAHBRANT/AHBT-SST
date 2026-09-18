using AAHBRANT.SST.Domain.Entidades;

namespace AAHBRANT.SST.Application.Novidades;

internal static class NovidadesMapper
{
    public static NovidadeVersaoDto Mapear(NovidadeVersao n) => new()
    {
        Id = n.Id,
        Titulo = n.Titulo,
        Versao = n.Versao,
        DataPublicacao = n.DataPublicacao,
        // Filtra Ativo explicitamente: depois de um Update, a coleção de navegação em memória ainda
        // contém os itens antigos soft-deletados (Ativo=false) ao lado dos novos, já que o
        // HasQueryFilter global só se aplica a novas consultas ao banco, não ao ChangeTracker.
        Itens = n.Itens
            .Where(i => i.Ativo)
            .OrderBy(i => i.Ordem)
            .Select(i => new NovidadeVersaoItemDto
            {
                Id = i.Id,
                Categoria = i.Categoria,
                Descricao = i.Descricao,
                Antes = i.Antes,
                Agora = i.Agora,
                Ordem = i.Ordem,
            })
            .ToList(),
    };
}
