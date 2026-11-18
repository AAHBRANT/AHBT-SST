using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Adesivo;

// PDF assinado da inspeção aberto pelo QR do adesivo, sem login (decisão do usuário em 18/11/2026; LGPD a validar).
// Devolve o PDF GUARDADO na finalização da assinatura (DocumentoAssinatura.PdfConteudo): nada é gerado na hora e
// nenhuma consulta depende do filtro global de obra, que nega tudo numa requisição anônima. O acesso é decidido pelo
// UID da tag (128 bits aleatórios) via ResolverEquipamentoPublicoQuery: tag Desativada/Perdida ou objeto excluído
// não abre nada.
public static class DocumentoPublicoInspecao
{
    public static IQueryable<DocumentoAssinatura> Consulta(IAppDbContext db, Guid inspecaoId)
        => db.DocumentosAssinatura.IgnoreQueryFilters()
            .Where(d => d.Ativo
                && d.EntidadeTipo == nameof(Inspecao)
                && d.EntidadeId == inspecaoId
                && d.Status == StatusDocumentoAssinatura.Finalizado
                && d.PdfConteudo != null);
}

public record ObterDocumentoEquipamentoPublicoQuery(string Uid, string? ProtocoloAdesivo) : IRequest<DocumentoEquipamentoPublico?>;

public record DocumentoEquipamentoPublico(byte[] Conteudo, string NomeArquivo);

public class ObterDocumentoEquipamentoPublicoQueryHandler : IRequestHandler<ObterDocumentoEquipamentoPublicoQuery, DocumentoEquipamentoPublico?>
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;

    public ObterDocumentoEquipamentoPublicoQueryHandler(IMediator mediator, IAppDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    public async Task<DocumentoEquipamentoPublico?> Handle(ObterDocumentoEquipamentoPublicoQuery request, CancellationToken ct)
    {
        var equipamento = await _mediator.Send(new ResolverEquipamentoPublicoQuery(request.Uid, request.ProtocoloAdesivo), ct);
        if (equipamento is null || !equipamento.TemDocumento) return null;

        var pdf = await DocumentoPublicoInspecao.Consulta(_db, equipamento.InspecaoDocumentoId)
            .OrderByDescending(d => d.FinalizadoEm)
            .Select(d => d.PdfConteudo)
            .FirstOrDefaultAsync(ct);
        if (pdf is not { Length: > 0 }) return null;

        var nome = string.IsNullOrWhiteSpace(equipamento.Protocolo) ? "inspecao" : equipamento.Protocolo;
        return new DocumentoEquipamentoPublico(pdf, $"{nome}.pdf");
    }
}
