using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Queries;

public record ObterFotoParticipanteQuery(Guid ParticipanteId) : IRequest<FotoParticipanteResultado?>;

public class FotoParticipanteResultado
{
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string NomeArquivo { get; set; } = string.Empty;
}

public class ObterFotoParticipanteQueryHandler : IRequestHandler<ObterFotoParticipanteQuery, FotoParticipanteResultado?>
{
    private readonly IAppDbContext _db;

    public ObterFotoParticipanteQueryHandler(IAppDbContext db) => _db = db;

    public async Task<FotoParticipanteResultado?> Handle(ObterFotoParticipanteQuery request, CancellationToken ct)
    {
        var participante = await _db.DdsParticipantes
            .FirstOrDefaultAsync(p => p.Id == request.ParticipanteId, ct);

        if (participante is null || participante.FotoConteudo.Length == 0) return null;
        if (!await _db.ObraDoPaiNoEscopoAsync(
                _db.Dds.IgnoreQueryFilters().Where(d => d.Id == participante.DdsId).Select(d => d.ObraId), ct))
            return null;

        var extensao = participante.FotoContentType == "image/png" ? "png" : "jpg";
        return new FotoParticipanteResultado
        {
            Conteudo = participante.FotoConteudo,
            ContentType = string.IsNullOrEmpty(participante.FotoContentType) ? "application/octet-stream" : participante.FotoContentType,
            NomeArquivo = $"dds-participante-{participante.Id}.{extensao}",
        };
    }
}
