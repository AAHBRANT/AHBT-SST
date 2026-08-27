using AAHBRANT.SST.Application.Asos;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.EntregasEpi;
using AAHBRANT.SST.Application.Treinamentos;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Queries;

// Perfil de Vida do Trabalhador — endpoint agregador que consolida em uma única chamada tudo que hoje
// está espalhado em abas com fetch próprio (ASO/Treinamentos/EPI), mais as seções novas (Riscos,
// Ocorrências, Cofre de Assinaturas). As sub-listas reaproveitam exatamente as mesmas projeções de
// ListarAsosQuery/ListarTreinamentosQuery/ListarEntregasEpiQuery — não chamamos essas queries via
// IMediator (não é usado dentro de handlers da Application neste projeto), então replicamos a
// projeção aqui, contra o mesmo IAppDbContext, com awaits sequenciais (mesmo DbContext).
public record PerfilCompletoTrabalhadorDto(
    Guid Id,
    string Nome,
    string Matricula,
    string Cpf,
    string? Rg,
    Guid ObraId,
    string ObraNome,
    Guid FuncaoId,
    string FuncaoNome,
    TipoVinculo Vinculo,
    DateTime DataAdmissao,
    string StatusAptidao,
    List<AsoDto> Asos,
    List<EntregaEpiDto> EpisAtivos,
    List<FrequenciaTrocaEpiDto> FrequenciaTrocas,
    List<TreinamentoDto> Treinamentos,
    AssiduidadeDdsDto AssiduidadeDds,
    List<RiscoExpostoDto> Riscos,
    List<OcorrenciaDto> Ocorrencias,
    List<AssinaturaPerfilDto> Assinaturas);

public record FrequenciaTrocaEpiDto(Guid CatalogoEpiId, string CatalogoEpiNome, int QuantidadeTrocas);

public record AssiduidadeDdsDto(int TotalRealizados, int TotalParticipados);

public record RiscoExpostoDto(
    Guid RiscoId,
    string PerigoNome,
    string AtividadeNome,
    string? Ambiente,
    string? Exposicao,
    string? Consequencia,
    int Probabilidade,
    int Severidade,
    NivelRisco NivelRisco,
    string? ControlesExistentes,
    string? ControlesAdicionais,
    StatusControleRisco Status);

public record OcorrenciaDto(
    Guid Id,
    TipoOcorrencia Tipo,
    DateTime Data,
    string Local,
    string Descricao,
    GravidadeAcidente Gravidade,
    bool HouveAfastamento,
    int? DiasAfastamento,
    StatusAcidente Status);

public record AssinaturaPerfilDto(
    Guid DocumentoAssinaturaId,
    string EntidadeTipo,
    Guid EntidadeId,
    MetodoAutenticacaoAssinatura Metodo,
    DateTime AssinadoEm,
    string? IpAddress,
    bool TemPdf);

public record ObterPerfilCompletoTrabalhadorQuery(Guid Id) : IRequest<PerfilCompletoTrabalhadorDto?>;

public class ObterPerfilCompletoTrabalhadorQueryHandler : IRequestHandler<ObterPerfilCompletoTrabalhadorQuery, PerfilCompletoTrabalhadorDto?>
{
    private readonly IAppDbContext _db;

    public ObterPerfilCompletoTrabalhadorQueryHandler(IAppDbContext db) => _db = db;

    public async Task<PerfilCompletoTrabalhadorDto?> Handle(ObterPerfilCompletoTrabalhadorQuery request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores
            .Where(t => t.Id == request.Id)
            .Select(t => new
            {
                t.Id,
                t.Nome,
                t.Matricula,
                t.Cpf,
                t.Rg,
                t.ObraId,
                t.FuncaoId,
                t.Vinculo,
                t.DataAdmissao,
                ObraNome = t.Obra!.Nome,
                FuncaoNome = t.Funcao!.Nome,
            })
            .FirstOrDefaultAsync(ct);

        if (trabalhador is null)
            return null;

        var asos = await _db.Asos
            .Where(a => a.TrabalhadorId == request.Id)
            .OrderByDescending(a => a.DataValidade)
            .Select(a => new AsoDto
            {
                Id = a.Id,
                TrabalhadorId = a.TrabalhadorId,
                Tipo = a.Tipo,
                DataExame = a.DataExame,
                DataValidade = a.DataValidade,
                ResultadoStatus = a.ResultadoStatus,
                MedicoNome = a.MedicoNome,
                MedicoCrm = a.MedicoCrm,
                ObservacoesClinicas = a.ObservacoesClinicas
            })
            .ToListAsync(ct);

        // Badge de aptidão: resultado do ASO mais recente por DataExame (o ASO mais recente pode não
        // ser o de maior DataValidade — ex.: um retorno ao trabalho reexamina antes do vencimento).
        var asoMaisRecente = asos.OrderByDescending(a => a.DataExame).FirstOrDefault();
        var statusAptidao = asoMaisRecente switch
        {
            null => "Sem ASO",
            { ResultadoStatus: ResultadoAso.Apto } => "Apto",
            { ResultadoStatus: ResultadoAso.AptoComRestricao } => "Apto com restrição",
            { ResultadoStatus: ResultadoAso.Inapto } => "Inapto",
            _ => "Pendente",
        };

        var entregasEpi = await _db.EntregasEpi
            .Where(x => x.TrabalhadorId == request.Id)
            .OrderByDescending(x => x.DataEntrega)
            .Select(x => new EntregaEpiDto(
                x.Id,
                x.TrabalhadorId,
                x.CatalogoEpiId,
                x.DataEntrega,
                x.DataDevolucao,
                x.DataValidade,
                x.Quantidade,
                x.QuantidadeDevolucao,
                x.VistoConsorcioResponsavel,
                x.Motivo,
                x.Observacoes,
                x.MotivoTipo,
                x.NumeroListaPresencaNr6,
                x.DataTreinamentoNr6))
            .ToListAsync(ct);

        var episAtivos = entregasEpi.Where(e => e.DataDevolucao is null).ToList();

        // GroupBy com a chave derivada de uma navegação (CatalogoEpi!.Nome) combinado com Select
        // construindo o record via construtor posicional não é traduzível pelo EF Core aqui — separamos
        // em duas consultas simples (contagem por CatalogoEpiId, depois nomes) e juntamos em memória.
        var contagemPorCatalogoEpi = await _db.EntregasEpi
            .Where(x => x.TrabalhadorId == request.Id)
            .GroupBy(x => x.CatalogoEpiId)
            .Select(g => new { CatalogoEpiId = g.Key, Quantidade = g.Count() })
            .ToListAsync(ct);

        var catalogoEpiIds = contagemPorCatalogoEpi.Select(c => c.CatalogoEpiId).ToList();
        var nomesCatalogoEpi = await _db.CatalogoEpis
            .Where(c => catalogoEpiIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Nome, ct);

        var frequenciaTrocas = contagemPorCatalogoEpi
            .Select(c => new FrequenciaTrocaEpiDto(c.CatalogoEpiId, nomesCatalogoEpi.GetValueOrDefault(c.CatalogoEpiId, "—"), c.Quantidade))
            .OrderByDescending(f => f.QuantidadeTrocas)
            .ToList();

        var treinamentos = await _db.Treinamentos
            .Where(x => x.TrabalhadorId == request.Id)
            .OrderByDescending(x => x.DataValidade)
            .Select(x => new TreinamentoDto(
                x.Id,
                x.TrabalhadorId,
                x.CursoTreinamentoId,
                x.DataRealizacao,
                x.DataValidade,
                x.CargaHorariaRealizada,
                x.InstituicaoInstrutor,
                x.NumeroCertificado))
            .ToListAsync(ct);

        var totalDdsRealizados = await _db.Dds
            .CountAsync(d => d.ObraId == trabalhador.ObraId && d.Data >= trabalhador.DataAdmissao, ct);
        var totalDdsParticipados = await _db.DdsParticipantes
            .CountAsync(p => p.TrabalhadorId == request.Id
                && p.Dds!.ObraId == trabalhador.ObraId
                && p.Dds.Data >= trabalhador.DataAdmissao, ct);

        var riscos = await _db.RiscoTrabalhadorExpostos
            .Where(r => r.TrabalhadorId == request.Id)
            .Select(r => new RiscoExpostoDto(
                r.RiscoId,
                r.Risco!.Perigo!.Nome,
                r.Risco.Atividade!.Nome,
                r.Risco.Ambiente,
                r.Risco.Exposicao,
                r.Risco.Consequencia,
                r.Risco.Probabilidade,
                r.Risco.Severidade,
                r.Risco.NivelRisco,
                r.Risco.ControlesExistentes,
                r.Risco.ControlesAdicionais,
                r.Risco.Status))
            .ToListAsync(ct);

        var ocorrencias = await _db.Acidentes
            .Where(a => a.TrabalhadorId == request.Id)
            .OrderByDescending(a => a.Data)
            .Select(a => new OcorrenciaDto(
                a.Id,
                a.Tipo,
                a.Data,
                a.Local,
                a.Descricao,
                a.Gravidade,
                a.HouveAfastamento,
                a.DiasAfastamento,
                a.Status))
            .ToListAsync(ct);

        var assinaturas = await _db.DocumentoSignatarios
            .Where(s => s.TrabalhadorId == request.Id)
            .OrderByDescending(s => s.AssinadoEm)
            .Select(s => new AssinaturaPerfilDto(
                s.DocumentoAssinaturaId,
                s.DocumentoAssinatura!.EntidadeTipo,
                s.DocumentoAssinatura.EntidadeId,
                s.MetodoAutenticacao,
                s.AssinadoEm,
                s.IpAddress,
                s.DocumentoAssinatura.PdfConteudo != null))
            .ToListAsync(ct);

        return new PerfilCompletoTrabalhadorDto(
            trabalhador.Id,
            trabalhador.Nome,
            trabalhador.Matricula,
            trabalhador.Cpf,
            trabalhador.Rg,
            trabalhador.ObraId,
            trabalhador.ObraNome,
            trabalhador.FuncaoId,
            trabalhador.FuncaoNome,
            trabalhador.Vinculo,
            trabalhador.DataAdmissao,
            statusAptidao,
            asos,
            episAtivos,
            frequenciaTrocas,
            treinamentos,
            new AssiduidadeDdsDto(totalDdsRealizados, totalDdsParticipados),
            riscos,
            ocorrencias,
            assinaturas);
    }
}
