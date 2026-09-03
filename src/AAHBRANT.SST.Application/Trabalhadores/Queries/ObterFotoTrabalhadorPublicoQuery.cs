using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Queries;

// Foto do "crachá digital" público — mesmo cuidado de ResolverTrabalhadorPublicoQuery: só resolve
// pelo Uid opaco da tag, nunca pelo Id (Guid) do trabalhador direto, pra manter uma única porta de
// entrada auditável/controlável para essa rota sem login (a tag precisa estar vinculada e existir).
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

        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == tag.EntidadeVinculadaId.Value, ct);
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
