using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Queries;

public record ExportarInspecaoPdfQuery(Guid Id) : IRequest<byte[]?>;

public class ExportarInspecaoPdfQueryHandler : IRequestHandler<ExportarInspecaoPdfQuery, byte[]?>
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly IInspecaoPdfService _pdf;
    private readonly IRegistradorRastreabilidadeService _rastreabilidade;

    public ExportarInspecaoPdfQueryHandler(IMediator mediator, IAppDbContext db, IInspecaoPdfService pdf, IRegistradorRastreabilidadeService rastreabilidade)
    {
        _mediator = mediator;
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
    }

    public async Task<byte[]?> Handle(ExportarInspecaoPdfQuery request, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterInspecaoDetalheQuery(request.Id), ct);
        if (detalhe is null) return null;

        // O DTO de detalhe só carrega TemFoto/TemFotoDepois (booleans) — os bytes das fotos são
        // buscados à parte aqui, só para o PDF, mesmo raciocínio de ExportarFichaEpiTrabalhadorQuery.
        var fotosPorResposta = await _db.InspecaoItemRespostas
            .Where(r => r.InspecaoId == request.Id)
            .Select(r => new { r.Id, r.FotoConteudo, r.FotoDepoisConteudo })
            .ToDictionaryAsync(r => r.Id, ct);

        var itens = detalhe.Respostas.Select(r =>
        {
            fotosPorResposta.TryGetValue(r.Id, out var fotos);
            return new InspecaoPdfItemModelo(
                r.Ordem,
                r.Descricao,
                r.Local,
                r.StatusItem,
                r.Observacao,
                r.PlanoDeAcao,
                r.ResponsavelUsuarioNome,
                r.Prazo,
                fotos?.FotoConteudo is { Length: > 0 } antes ? antes : null,
                fotos?.FotoDepoisConteudo is { Length: > 0 } depois ? depois : null);
        }).ToList();

        var inspecao = await _db.Inspecoes.FirstAsync(i => i.Id == request.Id, ct);
        var rastreio = await _rastreabilidade.GarantirAsync(nameof(Inspecao), request.Id, ct);

        var modelo = new InspecaoPdfModelo(
            detalhe.Inspecao.ObraNome,
            DescreverTipoInspecao(detalhe.Inspecao.TipoInspecao),
            detalhe.Inspecao.ChecklistModeloNome,
            detalhe.Inspecao.ChecklistModeloVersao,
            detalhe.Inspecao.Data,
            detalhe.Inspecao.ResponsavelUsuarioNome,
            detalhe.Inspecao.Status == StatusInspecao.Concluida ? "Concluída" : "Em andamento",
            itens,
            inspecao.NumeroDocumento,
            rastreio.ConteudoHash,
            rastreio.UrlValidacaoPublica,
            rastreio.QrCodePng,
            rastreio.TemAssinatura);

        return _pdf.Gerar(modelo);
    }

    private static string DescreverTipoInspecao(TipoInspecao tipo) => tipo switch
    {
        TipoInspecao.Obra => "Obra",
        TipoInspecao.Canteiro => "Canteiro de obras",
        TipoInspecao.Epi => "EPI",
        TipoInspecao.Epc => "EPC",
        TipoInspecao.Maquinas => "Máquinas",
        TipoInspecao.Ferramentas => "Ferramentas",
        TipoInspecao.Andaimes => "Andaimes",
        TipoInspecao.Escadas => "Escadas",
        TipoInspecao.Eletrica => "Instalações elétricas",
        TipoInspecao.Altura => "Trabalho em altura",
        TipoInspecao.EspacoConfinado => "Espaço confinado",
        TipoInspecao.Comportamental => "Comportamental",
        TipoInspecao.Terceiros => "Terceiros",
        _ => tipo.ToString(),
    };
}
