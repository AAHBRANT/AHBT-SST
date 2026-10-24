using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Queries;

// Foto do "crachá digital" — mesmo cuidado de ResolverTrabalhadorPublicoQuery: só resolve
// pelo Uid opaco da tag, nunca pelo Id (Guid) do trabalhador direto, pra manter uma única porta de
// entrada auditável/controlável (a tag precisa estar vinculada e existir; a API exige login).
public record ObterFotoTrabalhadorPublicoQuery(string Uid) : IRequest<FotoTrabalhadorResultado?>;

public class ObterFotoTrabalhadorPublicoQueryHandler : IRequestHandler<ObterFotoTrabalhadorPublicoQuery, FotoTrabalhadorResultado?>
{
    private readonly IAppDbContext _db;

    public ObterFotoTrabalhadorPublicoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<FotoTrabalhadorResultado?> Handle(ObterFotoTrabalhadorPublicoQuery request, CancellationToken ct)
    {
        var tag = await _db.TagsIdentificacao.FirstOrDefaultAsync(t => t.Uid == request.Uid, ct);
        if (tag is not { EntidadeVinculadaTipo: TipoEntidadeVinculada.Trabalhador, EntidadeVinculadaId: not null })
            return null;

        // IgnoreQueryFilters() necessário — ver comentário em ResolverTrabalhadorPublicoQuery.cs
        // (mesmo filtro global de RBAC/obra nega tudo em requisição anônima). Ativo reaplicado
        // manualmente: funcionário desativado não mantém foto pública.
        var trabalhador = await _db.Trabalhadores
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tag.EntidadeVinculadaId.Value && t.Ativo, ct);
        if (trabalhador is null || trabalhador.FotoConteudo is null || trabalhador.FotoConteudo.Length == 0) return null;

        var extensao = trabalhador.FotoContentType == "image/png" ? "png" : "jpg";
        return new FotoTrabalhadorResultado
        {
            Conteudo = trabalhador.FotoConteudo,
            ContentType = string.IsNullOrEmpty(trabalhador.FotoContentType) ? "application/octet-stream" : trabalhador.FotoContentType,
            NomeArquivo = $"trabalhador-{trabalhador.Id}-foto.{extensao}",
        };
    }
}
