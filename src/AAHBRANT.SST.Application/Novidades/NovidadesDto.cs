using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Novidades;

public class NovidadeVersaoItemDto
{
    public Guid Id { get; set; }
    public CategoriaNovidade Categoria { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string? Antes { get; set; }
    public string? Agora { get; set; }
    public int Ordem { get; set; }
}

public class NovidadeVersaoDto
{
    public Guid Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Versao { get; set; } = string.Empty;
    public DateTime DataPublicacao { get; set; }
    public List<NovidadeVersaoItemDto> Itens { get; set; } = new();

    // Só preenchido pela query "pendente" (o único caller que resolve um usuário atual) — o nome
    // vem de Usuario.Nome (backend, resolvido via claim oid/preferred_username do Entra ID), fonte
    // muito mais confiável do que o displayName do Teams SDK no cliente, que já provou vir vazio em
    // produção (ver comentário em AppShell.tsx). Nulo nos demais endpoints (Listar/Criar/etc.).
    public string? NomeUsuario { get; set; }
}

public class NovidadeVersaoItemInput
{
    public CategoriaNovidade Categoria { get; set; } = CategoriaNovidade.Melhoria;
    public string Descricao { get; set; } = string.Empty;
    public string? Antes { get; set; }
    public string? Agora { get; set; }
}
