using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Queries;

public record ExportarAtaEleicaoCipaPdfQuery(Guid ProcessoEleitoralId) : IRequest<byte[]?>;

public class ExportarAtaEleicaoCipaPdfQueryHandler : IRequestHandler<ExportarAtaEleicaoCipaPdfQuery, byte[]?>
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly ICipaPdfService _pdf;
    private readonly IRegistradorRastreabilidadeService _rastreabilidade;

    public ExportarAtaEleicaoCipaPdfQueryHandler(IMediator mediator, IAppDbContext db, ICipaPdfService pdf, IRegistradorRastreabilidadeService rastreabilidade)
    {
        _mediator = mediator;
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
    }

    public async Task<byte[]?> Handle(ExportarAtaEleicaoCipaPdfQuery request, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterProcessoEleitoralCipaDetalheQuery(request.ProcessoEleitoralId), ct);
        if (detalhe is null) return null;

        var logoConteudo = await _db.Obras
            .Where(o => o.Id == detalhe.Processo.ObraId)
            .Select(o => o.LogoConteudo)
            .FirstOrDefaultAsync(ct);

        var statusLabel = new Dictionary<StatusCandidatoCipa, string>
        {
            [StatusCandidatoCipa.Eleito] = "Eleito (titular)",
            [StatusCandidatoCipa.Suplente] = "Eleito (suplente)",
            [StatusCandidatoCipa.NaoEleito] = "Não eleito",
            [StatusCandidatoCipa.Deferido] = "Aguardando apuração",
            [StatusCandidatoCipa.Inscrito] = "Aguardando deferimento",
            [StatusCandidatoCipa.Indeferido] = "Inscrição indeferida",
        };

        var candidatos = detalhe.Candidatos
            .Select(c => new AtaEleicaoCipaCandidatoModelo(c.TrabalhadorNome, c.TrabalhadorMatricula, c.VotosRecebidos, statusLabel[c.Status]))
            .ToList();

        var rastreio = await _rastreabilidade.GarantirAsync(nameof(ProcessoEleitoralCipa), request.ProcessoEleitoralId, ct);

        var modelo = new AtaEleicaoCipaPdfModelo(
            detalhe.Processo.ObraNome,
            logoConteudo,
            detalhe.Processo.NumeroDocumento,
            detalhe.Processo.DataConvocacao,
            detalhe.Processo.DataVotacao,
            detalhe.Processo.DataApuracao,
            candidatos,
            rastreio.ConteudoHash,
            rastreio.UrlValidacaoPublica,
            rastreio.QrCodePng);

        return _pdf.GerarAtaEleicao(modelo);
    }
}

public record ExportarAtaReuniaoCipaPdfQuery(Guid ReuniaoId) : IRequest<byte[]?>;

public class ExportarAtaReuniaoCipaPdfQueryHandler : IRequestHandler<ExportarAtaReuniaoCipaPdfQuery, byte[]?>
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly ICipaPdfService _pdf;
    private readonly IRegistradorRastreabilidadeService _rastreabilidade;

    public ExportarAtaReuniaoCipaPdfQueryHandler(IMediator mediator, IAppDbContext db, ICipaPdfService pdf, IRegistradorRastreabilidadeService rastreabilidade)
    {
        _mediator = mediator;
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
    }

    public async Task<byte[]?> Handle(ExportarAtaReuniaoCipaPdfQuery request, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterReuniaoCipaDetalheQuery(request.ReuniaoId), ct);
        if (detalhe is null) return null;

        var logoConteudo = await _db.Obras
            .Where(o => o.Id == detalhe.Reuniao.ObraId)
            .Select(o => o.LogoConteudo)
            .FirstOrDefaultAsync(ct);

        var participantes = detalhe.Participantes
            .Select(p => new AtaReuniaoCipaParticipanteModelo(p.TrabalhadorNome, p.Presente))
            .ToList();

        var rastreio = await _rastreabilidade.GarantirAsync(nameof(ReuniaoCipa), request.ReuniaoId, ct);

        var modelo = new AtaReuniaoCipaPdfModelo(
            detalhe.Reuniao.ObraNome,
            logoConteudo,
            detalhe.Reuniao.Tipo == TipoReuniaoCipa.Ordinaria ? "Reunião Ordinária" : "Reunião Extraordinária",
            detalhe.Reuniao.DataReuniao,
            detalhe.Reuniao.Pauta,
            detalhe.Reuniao.Deliberacoes,
            participantes,
            rastreio.ConteudoHash,
            rastreio.UrlValidacaoPublica,
            rastreio.QrCodePng);

        return _pdf.GerarAtaReuniao(modelo);
    }
}
