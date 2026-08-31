using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Queries;

public record ObterFotoDepoisItemInspecaoQuery(Guid RespostaId) : IRequest<FotoItemInspecaoResultado?>;

public class ObterFotoDepoisItemInspecaoQueryHandler : IRequestHandler<ObterFotoDepoisItemInspecaoQuery, FotoItemInspecaoResultado?>
{
    private readonly IAppDbContext _db;

    public ObterFotoDepoisItemInspecaoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<FotoItemInspecaoResultado?> Handle(ObterFotoDepoisItemInspecaoQuery request, CancellationToken ct)
    {
        var resposta = await _db.InspecaoItemRespostas.FirstOrDefaultAsync(r => r.Id == request.RespostaId, ct);
        if (resposta is null || resposta.FotoDepoisConteudo is null || resposta.FotoDepoisConteudo.Length == 0) return null;

        var extensao = resposta.FotoDepoisContentType == "image/png" ? "png" : "jpg";
        return new FotoItemInspecaoResultado
        {
            Conteudo = resposta.FotoDepoisConteudo,
            ContentType = string.IsNullOrEmpty(resposta.FotoDepoisContentType) ? "application/octet-stream" : resposta.FotoDepoisContentType,
            NomeArquivo = $"inspecao-item-{resposta.Id}-depois.{extensao}",
        };
    }
}
