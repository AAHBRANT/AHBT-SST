using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Acidentes.Queries;

public record ExportarAcidentePdfQuery(Guid Id) : IRequest<byte[]>;

public class ExportarAcidentePdfQueryHandler : IRequestHandler<ExportarAcidentePdfQuery, byte[]>
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly IAcidentePdfService _pdf;
    private readonly IRegistradorRastreabilidadeService _rastreabilidade;

    public ExportarAcidentePdfQueryHandler(IMediator mediator, IAppDbContext db, IAcidentePdfService pdf, IRegistradorRastreabilidadeService rastreabilidade)
    {
        _mediator = mediator;
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
    }

    public async Task<byte[]> Handle(ExportarAcidentePdfQuery request, CancellationToken ct)
    {
        // Mesmo detalhe da tela (lança KeyNotFound se não existir); as fotos são lidas à parte, em bytes.
        var detalhe = await _mediator.Send(new ObterAcidenteDetalheQuery(request.Id), ct);
        var a = detalhe.Acidente;

        var logo = await _db.Obras.Where(o => o.Id == a.ObraId).Select(o => o.LogoConteudo).FirstOrDefaultAsync(ct);
        var fotos = await _db.AcidentesFotos
            .Where(f => f.AcidenteId == request.Id)
            .OrderBy(f => f.Ordem)
            .Select(f => f.FotoConteudo)
            .ToListAsync(ct);

        var envolvidos = a.Envolvidos.Select(e => e.Nome).ToList();
        if (envolvidos.Count == 0 && a.TrabalhadorNome is not null)
            envolvidos.Add(a.TrabalhadorNome);

        var rastreio = await _rastreabilidade.GarantirAsync(nameof(Acidente), request.Id, ct);

        var modelo = new AcidentePdfModelo(
            a.ObraNome,
            logo,
            DescreverTipo(a.Tipo),
            DescreverStatus(a.Status),
            a.Data,
            a.Hora,
            a.Local,
            a.AtividadeNome,
            envolvidos,
            a.Descricao,
            a.Lesao,
            a.RegioesCorpo,
            a.Consequencia,
            a.Atendimento,
            a.HouveAfastamento,
            a.DiasAfastamento,
            DescreverGravidade(a.Gravidade),
            a.DiasDebitados,
            a.NumeroCat,
            a.MetodologiaInvestigacao is { } m ? DescreverMetodologia(m) : null,
            a.Causas,
            detalhe.AcoesPlano.Select(p => new AcidentePdfAcaoModelo(
                DescreverTipoAcao(p.Tipo), p.Descricao, DescreverPrioridade(p.Prioridade), p.Prazo,
                p.ResponsavelUsuarioNome, DescreverStatusAcao(p.Status))).ToList(),
            fotos.Where(f => f is { Length: > 0 }).ToList(),
            rastreio.ConteudoHash,
            rastreio.UrlValidacaoPublica,
            rastreio.QrCodePng,
            rastreio.TemAssinatura);

        var pdf = _pdf.Gerar(modelo);
        await _rastreabilidade.RegistrarArquivoAsync(rastreio.DocumentoId, pdf, ct);
        return pdf;
    }

    private static string DescreverTipo(TipoOcorrencia tipo) => tipo switch
    {
        TipoOcorrencia.Acidente => "Acidente",
        TipoOcorrencia.Incidente => "Incidente",
        TipoOcorrencia.QuaseAcidente => "Quase-acidente",
        TipoOcorrencia.CondicaoInsegura => "Condição insegura",
        TipoOcorrencia.AtoInseguro => "Ato inseguro",
        TipoOcorrencia.DoencaOcupacional => "Doença ocupacional",
        _ => tipo.ToString(),
    };

    private static string DescreverStatus(StatusAcidente status) => status switch
    {
        StatusAcidente.Registrado => "Registrado",
        StatusAcidente.EmInvestigacao => "Em investigação",
        StatusAcidente.Concluido => "Concluído",
        _ => status.ToString(),
    };

    private static string DescreverGravidade(GravidadeAcidente gravidade) => gravidade switch
    {
        GravidadeAcidente.SemAfastamento => "Sem afastamento",
        GravidadeAcidente.ComAfastamento => "Com afastamento",
        GravidadeAcidente.IncapacidadePermanenteParcial => "Incapacidade permanente parcial",
        GravidadeAcidente.IncapacidadePermanenteTotal => "Incapacidade permanente total",
        GravidadeAcidente.Obito => "Óbito",
        _ => gravidade.ToString(),
    };

    private static string DescreverMetodologia(MetodologiaInvestigacao metodologia) => metodologia switch
    {
        MetodologiaInvestigacao.AnaliseCausaRaiz => "Análise de causa raiz",
        MetodologiaInvestigacao.CincoPorques => "5 Porquês",
        MetodologiaInvestigacao.ArvoreDeCausas => "Árvore de causas",
        MetodologiaInvestigacao.FatoresContribuintes => "Fatores contribuintes",
        MetodologiaInvestigacao.FalhasDeBarreira => "Falhas de barreira",
        _ => metodologia.ToString(),
    };

    private static string DescreverTipoAcao(TipoAcaoPlano tipo) => tipo switch
    {
        TipoAcaoPlano.Corretiva => "Corretiva",
        TipoAcaoPlano.Preventiva => "Preventiva",
        TipoAcaoPlano.Melhoria => "Melhoria",
        _ => tipo.ToString(),
    };

    private static string DescreverPrioridade(PrioridadeAcao prioridade) => prioridade switch
    {
        PrioridadeAcao.Critica => "Crítica",
        PrioridadeAcao.Alta => "Alta",
        PrioridadeAcao.Media => "Média",
        PrioridadeAcao.Baixa => "Baixa",
        _ => prioridade.ToString(),
    };

    private static string DescreverStatusAcao(StatusControleRisco status) => status switch
    {
        StatusControleRisco.Pendente => "Pendente",
        StatusControleRisco.EmAndamento => "Em andamento",
        StatusControleRisco.Concluido => "Concluída",
        _ => status.ToString(),
    };
}
