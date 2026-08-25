using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Queries;

public record ObterFotoItemInspecaoQuery(Guid RespostaId) : IRequest<FotoItemInspecaoResultado?>;

public class FotoItemInspecaoResultado
{
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string NomeArquivo { get; set; } = string.Empty;
}

public class ObterFotoItemInspecaoQueryHandler : IRequestHandler<ObterFotoItemInspecaoQuery, FotoItemInspecaoResultado?>
{
    private readonly IAppDbContext _db;

    public ObterFotoItemInspecaoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<FotoItemInspecaoResultado?> Handle(ObterFotoItemInspecaoQuery request, CancellationToken ct)
    {
        var resposta = await _db.InspecaoItemRespostas.FirstOrDefaultAsync(r => r.Id == request.RespostaId, ct);
        if (resposta is null || resposta.FotoConteudo.Length == 0) return null;

        var extensao = resposta.FotoContentType == "image/png" ? "png" : "jpg";
        return new FotoItemInspecaoResultado
        {
            Conteudo = resposta.FotoConteudo,
            ContentType = string.IsNullOrEmpty(resposta.FotoContentType) ? "application/octet-stream" : resposta.FotoContentType,
            NomeArquivo = $"inspecao-item-{resposta.Id}.{extensao}",
        };
    }
}
