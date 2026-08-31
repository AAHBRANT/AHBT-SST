using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissoesTrabalho.Queries;

public record ExportarPermissaoTrabalhoPdfQuery(Guid Id) : IRequest<byte[]?>;

public class ExportarPermissaoTrabalhoPdfQueryHandler : IRequestHandler<ExportarPermissaoTrabalhoPdfQuery, byte[]?>
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly IPtPdfService _pdf;

    public ExportarPermissaoTrabalhoPdfQueryHandler(IMediator mediator, IAppDbContext db, IPtPdfService pdf)
    {
        _mediator = mediator;
        _db = db;
        _pdf = pdf;
    }

    public async Task<byte[]?> Handle(ExportarPermissaoTrabalhoPdfQuery request, CancellationToken ct)
    {
        var detalhe = await _mediator.Send(new ObterPermissaoTrabalhoDetalheQuery(request.Id), ct);
        if (detalhe is null) return null;

        // Ciência da equipe (§9) usa o Motor de Assinatura Eletrônica (DocumentoAssinatura), não um
        // campo próprio da PT — mesmo padrão já usado por Dds/EntregaEpi.
        var documento = await _db.DocumentosAssinatura
            .Include(d => d.Signatarios)
            .FirstOrDefaultAsync(d => d.EntidadeTipo == nameof(PermissaoTrabalho) && d.EntidadeId == request.Id, ct);
        var assinaram = documento?.Signatarios.Select(s => s.TrabalhadorId).ToHashSet() ?? new HashSet<Guid>();

        byte[]? logoConteudo = detalhe.PermissaoTrabalho.ObraId is { } obraId
            ? await _db.Obras.Where(o => o.Id == obraId).Select(o => o.LogoConteudo).FirstOrDefaultAsync(ct)
            : null;

        return _pdf.Gerar(MontarModelo(detalhe, assinaram, logoConteudo));
    }

    private static readonly Dictionary<ItemPreRequisitoPt, string> RotulosPreRequisito = new()
    {
        [ItemPreRequisitoPt.AprEspecificaRevisadaDisponivel] = "APR específica da atividade revisada e disponível",
        [ItemPreRequisitoPt.PgrInventarioRiscosCompativel] = "PGR / Inventário de Riscos compatível com a atividade",
        [ItemPreRequisitoPt.InspecoesChecklistsEquipamentosValidos] = "Inspeções / checklists dos equipamentos válidos",
        [ItemPreRequisitoPt.ProcedimentoInstrucaoTrabalhoAplicavelDisponivel] = "Procedimento / instrução de trabalho aplicável disponível",
        [ItemPreRequisitoPt.TrabalhadoresCapacitadosAutorizadosAptos] = "Trabalhadores capacitados, autorizados e aptos quando aplicável",
        [ItemPreRequisitoPt.PlanoEmergenciaMeiosComunicacaoConhecidos] = "Plano de emergência e meios de comunicação conhecidos pela equipe",
    };

    private static readonly Dictionary<TipoTrabalhoEspecialPt, string> RotulosTipoTrabalho = new()
    {
        [TipoTrabalhoEspecialPt.TrabalhoEmAltura] = "Trabalho em altura – NR-35",
        [TipoTrabalhoEspecialPt.TrabalhoAQuenteFonteIgnicao] = "Trabalho a quente / fonte de ignição",
        [TipoTrabalhoEspecialPt.BloqueioEnergiasPerigosas] = "Bloqueio de energias perigosas (LOTO)",
        [TipoTrabalhoEspecialPt.DemolicaoCortePerfuracao] = "Demolição / corte / perfuração",
        [TipoTrabalhoEspecialPt.EspacoConfinado] = "Espaço confinado – NR-33",
        [TipoTrabalhoEspecialPt.EscavacaoValaFundacao] = "Escavação / vala / fundação",
        [TipoTrabalhoEspecialPt.TrabalhoProximoTrafegoVias] = "Trabalho próximo a tráfego / vias",
        [TipoTrabalhoEspecialPt.MaquinasEquipamentos] = "Máquinas e equipamentos",
        [TipoTrabalhoEspecialPt.EletricidadeIntervencaoEletrica] = "Eletricidade / intervenção elétrica – NR-10",
        [TipoTrabalhoEspecialPt.MovimentacaoIcamentoCargas] = "Movimentação e içamento de cargas",
        [TipoTrabalhoEspecialPt.ProdutosQuimicosInflamaveis] = "Produtos químicos / inflamáveis",
        [TipoTrabalhoEspecialPt.Outro] = "Outro",
    };

    private static readonly Dictionary<ItemVerificacaoPt, string> RotulosVerificacao = new()
    {
        [ItemVerificacaoPt.AreaIsoladaSinalizadaAcessoControlado] = "Área isolada, sinalizada e com acesso controlado?",
        [ItemVerificacaoPt.AprDiscutidaComEquipeAntesDoInicio] = "APR discutida com toda a equipe antes do início?",
        [ItemVerificacaoPt.InterferenciasExistentesIdentificadas] = "Interferências existentes identificadas (redes, tubulações, energia, tráfego etc.)?",
        [ItemVerificacaoPt.FontesEnergiaIdentificadasBloqueadasTestadas] = "Fontes de energia identificadas, bloqueadas e testadas quando aplicável?",
        [ItemVerificacaoPt.MaquinasFerramentasAcessoriosInspecionados] = "Máquinas, ferramentas, acessórios e dispositivos inspecionados e adequados?",
        [ItemVerificacaoPt.EpcsInstaladosCondicoesUso] = "EPCs instalados e em condições de uso?",
        [ItemVerificacaoPt.EpisDisponiveisAdequadosCaValido] = "EPIs definidos na APR disponíveis, adequados, com CA válido quando aplicável?",
        [ItemVerificacaoPt.CondicoesAcessoCirculacaoIluminacaoOrganizacao] = "Condições de acesso, circulação, iluminação e organização adequadas?",
        [ItemVerificacaoPt.CondicoesMeteorologicasPermitemExecucaoSegura] = "Condições meteorológicas permitem execução segura da atividade?",
        [ItemVerificacaoPt.RiscoQuedaPessoasObjetosControlado] = "Risco de queda de pessoas/objetos controlado quando aplicável?",
        [ItemVerificacaoPt.RiscoIncendioExplosaoControladoExtintorDisponivel] = "Risco de incêndio/explosão controlado; extintor adequado disponível quando aplicável?",
        [ItemVerificacaoPt.AtmosferaAvaliadaMonitorada] = "Atmosfera avaliada/monitorada quando aplicável (O₂, inflamáveis e tóxicos)?",
        [ItemVerificacaoPt.EscavacoesTaludesEscoramentosAcessosInspecionados] = "Escavações/taludes/escoramentos/acessos inspecionados quando aplicável?",
        [ItemVerificacaoPt.PlanoIcamentoAcessoriosMovimentacaoVerificados] = "Plano de içamento e acessórios de movimentação verificados quando aplicável?",
        [ItemVerificacaoPt.VigiaObservadorSinaleiroApoioDefinido] = "Vigia, observador, sinaleiro ou trabalhador de apoio definido quando aplicável?",
    };

    private static readonly Dictionary<ItemEpiPt, string> RotulosEpi = new()
    {
        [ItemEpiPt.Capacete] = "Capacete",
        [ItemEpiPt.Oculos] = "Óculos",
        [ItemEpiPt.ProtetorFacial] = "Protetor facial",
        [ItemEpiPt.ProtetorAuditivo] = "Auditivo",
        [ItemEpiPt.Luvas] = "Luvas",
        [ItemEpiPt.Calcado] = "Calçado",
        [ItemEpiPt.Respirador] = "Respirador",
        [ItemEpiPt.CinturaoTalabarte] = "Cinturão/talabarte",
        [ItemEpiPt.VestimentaEspecifica] = "Vestimenta específica",
    };

    private static readonly Dictionary<ItemEpcPt, string> RotulosEpc = new()
    {
        [ItemEpcPt.IsolamentoBarreira] = "Isolamento/barreira",
        [ItemEpcPt.GuardaCorpo] = "Guarda-corpo",
        [ItemEpcPt.LinhaDeVida] = "Linha de vida",
        [ItemEpcPt.Extintor] = "Extintor",
        [ItemEpcPt.ExaustaoVentilacao] = "Exaustão/ventilação",
        [ItemEpcPt.DetectorGases] = "Detector de gases",
        [ItemEpcPt.KitResgate] = "Kit de resgate",
        [ItemEpcPt.Iluminacao] = "Iluminação",
        [ItemEpcPt.Sinalizacao] = "Sinalização",
    };

    private static PtPdfModelo MontarModelo(PermissaoTrabalhoDetalheDto detalhe, HashSet<Guid> assinaram, byte[]? obraLogoConteudo)
    {
        var pt = detalhe.PermissaoTrabalho;
        return new PtPdfModelo(
            pt.NumeroPt,
            pt.ObraNome,
            obraLogoConteudo,
            pt.DescricaoAtividade,
            pt.Local,
            pt.EmpresaExecutante,
            pt.Data,
            pt.HorarioInicio,
            pt.HorarioFim,
            pt.Validade,
            pt.ResponsavelExecucaoUsuarioNome,
            pt.ResponsavelAreaUsuarioNome,
            detalhe.PreRequisitos.Select(r => new PtPdfItemBinario(RotulosPreRequisito[r.Item], r.Atendido)).ToList(),
            detalhe.TiposTrabalho.Select(t => new PtPdfTipoTrabalho(RotulosTipoTrabalho[t.Tipo], t.DescricaoOutro)).ToList(),
            detalhe.Verificacoes.Select(v => new PtPdfVerificacao(RotulosVerificacao[v.Item], v.Resposta)).ToList(),
            detalhe.Epis.Select(e => new PtPdfEpi(RotulosEpi[e.Item], e.Complemento)).ToList(),
            pt.OutrosEpis,
            detalhe.Epcs.Select(e => RotulosEpc[e.Item]).ToList(),
            pt.OutrosEpcs,
            detalhe.RiscosCriticos.Select(r => new PtPdfRiscoCritico(r.RiscoCondicao, r.ControleComplementar, r.ResponsavelEvidencia)).ToList(),
            new PtPdfAssinatura(pt.AutorizadoPorUsuarioNome, pt.DataAutorizacao),
            new PtPdfAssinatura(pt.ResponsavelExecucaoUsuarioNome, pt.DataAssinaturaExecucao),
            pt.ResponsavelSstUsuarioId.HasValue ? new PtPdfAssinatura(pt.ResponsavelSstUsuarioNome, pt.DataAssinaturaSst) : null,
            pt.SuspensaPorUsuarioId.HasValue ? new PtPdfSuspensao(pt.SuspensaPorUsuarioNome, pt.DataSuspensao, pt.MotivoSuspensao) : null,
            pt.EncerradaPorUsuarioId.HasValue ? new PtPdfEncerramento(pt.EncerradaPorUsuarioNome, pt.DataEncerramento, pt.ObservacoesEncerramento) : null,
            detalhe.Responsaveis.Select(r => new PtPdfEnvolvido(r.TrabalhadorNome, r.TrabalhadorFuncaoNome, assinaram.Contains(r.TrabalhadorId))).ToList());
    }
}
