using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Queries;

// Situação da CIPA por obra (Etapa 1 do plano CIPA de 15/11/2026): conta automaticamente os
// empregados ativos de cada obra a partir do cadastro de trabalhadores e cruza com o último
// dimensionamento, os membros com mandato vigente e o processo eleitoral em andamento.
//
// Regras e limites (disclosure):
// - "Empregados" para fins de NR-5 = vínculo CLT sem data de demissão. Terceirizados contam para a
//   CIPA da empresa contratada, não desta — por isso aparecem em campo separado, só informativo
//   (a NR-5 exige que a contratada com 5+ empregados no local nomeie representante).
//   Estagiário, autônomo e prestador de serviço ficam fora da contagem.
// - O sistema NÃO decide sozinho o número de titulares/suplentes: o Quadro I (CNAE/grau de risco x
//   faixa de empregados) continua sendo informado no dimensionamento pelo técnico de SST. Aqui só se
//   usa o piso de 20 empregados — abaixo dele a NR-5 manda nomear representante; a partir dele a
//   obra precisa de um dimensionamento feito por profissional habilitado.
// - Dimensionamento "desatualizado" = o número de empregados mudou mais de 10% (ou cruzou a marca de
//   20) desde o último dimensionamento registrado. Heurística de alerta, não regra da norma.
public record ObterSituacaoCipaPorObraQuery : IRequest<List<SituacaoCipaObraDto>>;

public static class SituacaoCipaObra
{
    public const int PisoEmpregadosQuadroI = 20;
    public const int MinimoTerceirosParaRepresentante = 5;
}

public class ObterSituacaoCipaPorObraQueryHandler : IRequestHandler<ObterSituacaoCipaPorObraQuery, List<SituacaoCipaObraDto>>
{
    private readonly IAppDbContext _db;
    public ObterSituacaoCipaPorObraQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<SituacaoCipaObraDto>> Handle(ObterSituacaoCipaPorObraQuery request, CancellationToken ct)
    {
        var hoje = DateTime.UtcNow.Date;

        var obras = await _db.Obras
            .Where(o => o.Status == StatusObra.Planejada || o.Status == StatusObra.EmAndamento || o.Status == StatusObra.Paralisada)
            .Select(o => new { o.Id, o.Nome, o.Codigo, o.Status })
            .ToListAsync(ct);

        var contagens = await _db.Trabalhadores
            .Where(t => t.DataDemissao == null && (t.Vinculo == TipoVinculo.Clt || t.Vinculo == TipoVinculo.Terceirizado))
            .GroupBy(t => new { t.ObraId, t.Vinculo })
            .Select(g => new { g.Key.ObraId, g.Key.Vinculo, Total = g.Count() })
            .ToListAsync(ct);

        var dimensionamentos = await _db.DimensionamentosCipa
            .Select(d => new { d.ObraId, d.NumeroFuncionarios, d.NumeroTitulares, d.NumeroSuplentes, d.GrauRisco, d.DataCalculo })
            .ToListAsync(ct);

        var membros = await _db.MembrosCipa
            .Where(m => m.DataInicioMandato <= hoje && m.DataFimMandato >= hoje)
            .Select(m => new { m.ObraId, m.DataFimMandato })
            .ToListAsync(ct);

        var processos = await _db.ProcessosEleitoraisCipa
            .Where(p => p.Status != StatusProcessoEleitoralCipa.Encerrado)
            .Select(p => new { p.ObraId, p.Id, p.Status, p.DataVotacao, p.DataConvocacao })
            .ToListAsync(ct);

        var resultado = new List<SituacaoCipaObraDto>();
        foreach (var obra in obras)
        {
            var empregados = contagens.Where(c => c.ObraId == obra.Id && c.Vinculo == TipoVinculo.Clt).Sum(c => c.Total);
            var terceiros = contagens.Where(c => c.ObraId == obra.Id && c.Vinculo == TipoVinculo.Terceirizado).Sum(c => c.Total);
            var dim = dimensionamentos.Where(d => d.ObraId == obra.Id).OrderByDescending(d => d.DataCalculo).FirstOrDefault();
            var membrosObra = membros.Where(m => m.ObraId == obra.Id).ToList();
            var processo = processos.Where(p => p.ObraId == obra.Id).OrderByDescending(p => p.DataConvocacao).FirstOrDefault();

            var dto = new SituacaoCipaObraDto
            {
                ObraId = obra.Id,
                ObraNome = obra.Nome,
                ObraCodigo = obra.Codigo,
                EmpregadosAtivos = empregados,
                TerceirizadosAtivos = terceiros,
                MembrosComMandatoVigente = membrosObra.Count,
                FimMandatoVigente = membrosObra.Count > 0 ? membrosObra.Max(m => m.DataFimMandato) : null,
                ProcessoEleitoralId = processo?.Id,
                StatusProcessoEleitoral = processo?.Status,
                DataVotacao = processo?.DataVotacao,
                UltimoDimensionamentoData = dim?.DataCalculo,
                UltimoDimensionamentoFuncionarios = dim?.NumeroFuncionarios,
                UltimoDimensionamentoGrauRisco = dim?.GrauRisco,
                TitularesDimensionados = dim?.NumeroTitulares,
                SuplentesDimensionados = dim?.NumeroSuplentes,
                TerceirosExigemRepresentante = terceiros >= SituacaoCipaObra.MinimoTerceirosParaRepresentante,
            };

            dto.DimensionamentoDesatualizado = dim != null && DimensionamentoDesatualizado(dim.NumeroFuncionarios, empregados);
            dto.Situacao = Classificar(empregados, membrosObra.Count, processo != null, dto.FimMandatoVigente, hoje);
            resultado.Add(dto);
        }

        // Pior situação primeiro: o técnico abre a tela e vê o que exige ação no topo.
        return resultado
            .OrderBy(r => (int)r.Situacao)
            .ThenByDescending(r => r.EmpregadosAtivos)
            .ToList();
    }

    public static bool DimensionamentoDesatualizado(int dimensionados, int atuais)
    {
        var piso = SituacaoCipaObra.PisoEmpregadosQuadroI;
        if ((dimensionados < piso) != (atuais < piso)) return true;
        if (dimensionados <= 0) return atuais > 0;
        return Math.Abs(atuais - dimensionados) > dimensionados * 0.10;
    }

    public static SituacaoCipaObraTipo Classificar(int empregados, int membrosVigentes, bool processoAberto, DateTime? fimMandato, DateTime hoje)
    {
        if (empregados < SituacaoCipaObra.PisoEmpregadosQuadroI)
            return membrosVigentes > 0 ? SituacaoCipaObraTipo.RepresentanteNomeado : SituacaoCipaObraTipo.NomearRepresentante;

        if (processoAberto) return SituacaoCipaObraTipo.EleicaoEmAndamento;
        if (membrosVigentes == 0) return SituacaoCipaObraTipo.PrecisaDeCipa;

        // NR-5: convocação da eleição no mínimo 60 dias antes do fim do mandato.
        if (fimMandato.HasValue && (fimMandato.Value.Date - hoje).TotalDays <= 60)
            return SituacaoCipaObraTipo.ConvocarEleicao;

        return SituacaoCipaObraTipo.CipaAtiva;
    }
}

// Ordem numérica = prioridade de exibição (menor = mais urgente).
public enum SituacaoCipaObraTipo
{
    PrecisaDeCipa = 1,
    ConvocarEleicao = 2,
    NomearRepresentante = 3,
    EleicaoEmAndamento = 4,
    RepresentanteNomeado = 5,
    CipaAtiva = 6,
}

public class SituacaoCipaObraDto
{
    public Guid ObraId { get; set; }
    public string ObraNome { get; set; } = string.Empty;
    public string ObraCodigo { get; set; } = string.Empty;
    public int EmpregadosAtivos { get; set; }
    public int TerceirizadosAtivos { get; set; }
    public bool TerceirosExigemRepresentante { get; set; }
    public SituacaoCipaObraTipo Situacao { get; set; }
    public int MembrosComMandatoVigente { get; set; }
    public DateTime? FimMandatoVigente { get; set; }
    public Guid? ProcessoEleitoralId { get; set; }
    public StatusProcessoEleitoralCipa? StatusProcessoEleitoral { get; set; }
    public DateTime? DataVotacao { get; set; }
    public DateTime? UltimoDimensionamentoData { get; set; }
    public int? UltimoDimensionamentoFuncionarios { get; set; }
    public int? UltimoDimensionamentoGrauRisco { get; set; }
    public int? TitularesDimensionados { get; set; }
    public int? SuplentesDimensionados { get; set; }
    public bool DimensionamentoDesatualizado { get; set; }
}
