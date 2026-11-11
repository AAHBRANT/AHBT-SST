using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Integracoes;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Application.IntegracoesAtendimento.Queries;

// Entrada da tela: o que pode ser feito na obra (iniciar, continuar), quem aguarda integração e quem pode ser o instrutor.
public record ObterEntradaIntegracaoQuery(Guid ObraId, string? AzureAdObjectId = null, bool AutenticacaoHabilitada = false)
    : IRequest<EntradaIntegracaoDto>;

public class ObterEntradaIntegracaoQueryHandler : IRequestHandler<ObterEntradaIntegracaoQuery, EntradaIntegracaoDto>
{
    private readonly IAppDbContext _db;
    private readonly IOptions<OpcoesIntegracao> _opcoes;

    public ObterEntradaIntegracaoQueryHandler(IAppDbContext db, IOptions<OpcoesIntegracao> opcoes)
    {
        _db = db;
        _opcoes = opcoes;
    }

    public async Task<EntradaIntegracaoDto> Handle(ObterEntradaIntegracaoQuery request, CancellationToken ct)
    {
        await AutorizacaoIntegracaoAtendimento.AutorizarAsync(_db, request.ObraId, request.AzureAdObjectId, request.AutenticacaoHabilitada, ct);
        var obraNome = await _db.Obras.Where(o => o.Id == request.ObraId).Select(o => o.Nome).FirstAsync(ct);
        var habilitada = _opcoes.Value.AtendimentoHabilitado;
        var bloqueios = new List<BloqueioIntegracaoDto>();
        if (!habilitada)
            bloqueios.Add(new("FluxoNaoHabilitado", "O atendimento de integração ainda não está habilitado."));

        var emAndamento = await _db.IntegracoesAtendimento
            .Where(a => a.ObraId == request.ObraId && a.Status == StatusIntegracaoAtendimento.EmAndamento)
            .Select(a => new AtendimentoEmAndamentoResumoDto(a.Id, a.Numero, "emAndamento", a.EtapaAtualOrdem))
            .FirstOrDefaultAsync(ct);
        var participantes = emAndamento is null
            ? new HashSet<Guid>()
            : (await _db.ParticipantesIntegracao.Where(p => p.IntegracaoAtendimentoId == emAndamento.Id)
                .Select(p => p.TrabalhadorId).ToListAsync(ct)).ToHashSet();

        // Quem aguarda a integração: planos de entrada abertos da obra. Trabalhador/Função por consulta própria (sem INNER JOIN).
        var planos = await _db.PlanosIntegracao
            .Where(p => p.ObraId == request.ObraId && p.Status == StatusPlanoIntegracao.Aberto)
            .Select(p => new { p.Id, p.TrabalhadorId, p.FuncaoId, p.CreatedAtUtc }).ToListAsync(ct);
        var idsTrab = planos.Select(p => p.TrabalhadorId).ToList();
        var entidades = await _db.Trabalhadores
            .Where(t => idsTrab.Contains(t.Id) && t.Situacao == SituacaoTrabalhador.Ativo && t.Vinculo != TipoVinculo.PrestadorServico)
            .ToListAsync(ct);
        var trabalhadores = entidades.ToDictionary(t => t.Id, t => t.Nome);
        // Prévia das etapas por pessoa, pela mesma calculadora do Iniciar.
        var cursosCalc = await ExigenciasEtapasIntegracao.CarregarCursosAsync(_db, obraNome, ct);
        var pessoasCalc = await ExigenciasEtapasIntegracao.CarregarPessoasAsync(_db, entidades, request.ObraId, ct);
        var calculo = CalculadoraEtapasIntegracao.Calcular(pessoasCalc, cursosCalc);
        var porPessoa = entidades.ToDictionary(t => t.Id, t =>
        {
            var pessoa = pessoasCalc.First(x => x.TrabalhadorId == t.Id);
            return (IReadOnlyList<EtapaCandidatoDto>)CalculadoraEtapasIntegracao.Definicao.Select(d =>
                new EtapaCandidatoDto(TextoDto.Camel(d.Tipo.ToString()), d.Ordem,
                    calculo.First(c => c.Tipo == d.Tipo).Participantes.Contains(t.Id) ? "nestaIntegracao"
                    : d.Norma is not null && pessoa.NormasValidas.Contains(d.Norma) ? "valido" : "naoExigido")).ToList();
        });
        var funcoes = await _db.Funcoes.Select(f => new { f.Id, f.Nome }).ToDictionaryAsync(f => f.Id, f => f.Nome, ct);
        var candidatos = planos
            .Where(p => trabalhadores.ContainsKey(p.TrabalhadorId))
            .GroupBy(p => p.TrabalhadorId).Select(g => g.OrderByDescending(p => p.CreatedAtUtc).First())
            .Select(p => new CandidatoIntegracaoDto(
                p.TrabalhadorId, trabalhadores[p.TrabalhadorId], p.FuncaoId, funcoes.GetValueOrDefault(p.FuncaoId, string.Empty),
                p.Id, true, participantes.Contains(p.TrabalhadorId), porPessoa[p.TrabalhadorId]))
            .OrderBy(c => c.Nome).ToList();

        // Instrutores elegíveis: perfil de instrutor, ativo; de outra obra só com a autorização que o cadastro já guarda.
        var perfis = await _db.PerfisInstrutor.ToListAsync(ct);
        var idsInstr = perfis.Select(p => p.TrabalhadorId).ToList();
        var instrutoresTrab = await _db.Trabalhadores.IgnoreQueryFilters()
            .Where(t => idsInstr.Contains(t.Id) && t.Ativo && t.Situacao == SituacaoTrabalhador.Ativo)
            .Select(t => new { t.Id, t.Nome, t.ObraId }).ToListAsync(ct);
        var instrutores = (from p in perfis
                           join t in instrutoresTrab on p.TrabalhadorId equals t.Id
                           where t.ObraId == request.ObraId || (p.PodeAssinarEmOutrasObras && p.AutorizadoOutrasObrasEm != null)
                           orderby t.Nome
                           select new InstrutorIntegracaoDto(t.Id, t.Nome,
                               AAHBRANT.SST.Application.Instrutores.DescricaoInstrutor.Registro(p.Conselho, p.NumeroRegistro, p.UfRegistro)))
            .ToList();
        if (instrutores.Count == 0)
            bloqueios.Add(new("InstrutorNaoCadastrado", "Cadastre o perfil de instrutor de quem vai conduzir a integração.", "/pessoas"));

        // Cursos do catálogo: NR-06 e NR-18 são de todos; NR-12 e NR-35 só se alguém precisar (checado ao iniciar).
        var cursos = await ExigenciasEtapasIntegracao.CarregarCursosAsync(_db, obraNome, ct);
        foreach (var tipo in new[] { TipoEtapaIntegracao.Nr06, TipoEtapaIntegracao.Nr18 })
            if (!cursos.ContainsKey(tipo))
                bloqueios.Add(new("CursoNaoCadastrado",
                    $"O curso {CalculadoraEtapasIntegracao.NormaDoTipo(tipo)} não está cadastrado no catálogo de cursos.", "/gestao-sst?secao=treinamentos"));

        var podeIniciar = bloqueios.Count == 0 && emAndamento is null && candidatos.Any(c => !c.JaParticipa);
        return new EntradaIntegracaoDto(habilitada, request.ObraId, obraNome, emAndamento, podeIniciar, bloqueios, candidatos, instrutores);
    }
}

public record ObterIntegracaoQuery(Guid IntegracaoId, string? AzureAdObjectId = null, bool AutenticacaoHabilitada = false)
    : IRequest<IntegracaoAtendimentoDto?>;

public class ObterIntegracaoQueryHandler : IRequestHandler<ObterIntegracaoQuery, IntegracaoAtendimentoDto?>
{
    private readonly IAppDbContext _db;

    public ObterIntegracaoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<IntegracaoAtendimentoDto?> Handle(ObterIntegracaoQuery request, CancellationToken ct)
    {
        // Atendimento de outra obra fora do escopo do usuário (ou sem vínculo do técnico): 404.
        await AutorizacaoIntegracaoAtendimento.CarregarAsync(_db, request.IntegracaoId, request.AzureAdObjectId, request.AutenticacaoHabilitada, ct);
        return await MontadorIntegracaoAtendimento.MontarAsync(_db, request.IntegracaoId, ct);
    }
}

// Relatório de leitura (Administrador): turmas de integração ainda agendadas no modelo antigo (combo agendado pela
// ficha de entrada, fora de qualquer atendimento). Só consulta: decidir o destino delas fica para depois da entrega.
public record RelatorioTurmasIntegracaoAgendadasQuery : IRequest<List<TurmaIntegracaoAgendadaDto>>;

public class RelatorioTurmasIntegracaoAgendadasQueryHandler : IRequestHandler<RelatorioTurmasIntegracaoAgendadasQuery, List<TurmaIntegracaoAgendadaDto>>
{
    private readonly IAppDbContext _db;

    public RelatorioTurmasIntegracaoAgendadasQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<TurmaIntegracaoAgendadaDto>> Handle(RelatorioTurmasIntegracaoAgendadasQuery request, CancellationToken ct)
    {
        var cursos = await _db.CursosTreinamento.ToListAsync(ct);
        var doCombo = cursos.Where(c => c.EhIntegracaoSeguranca || PacoteIntegracao.EhCursoDoPacote(c)).ToDictionary(c => c.Id, c => c.Nome);
        var ids = doCombo.Keys.ToList();
        var turmas = await _db.SessoesTreinamento
            .Where(s => s.IntegracaoAtendimentoId == null && s.Status == StatusSessaoTreinamento.EmAndamento && ids.Contains(s.CursoTreinamentoId))
            .Select(s => new { s.Id, s.ObraId, s.CursoTreinamentoId, s.DataRealizacao })
            .ToListAsync(ct);
        var turmaIds = turmas.Select(t => t.Id).ToList();
        var inscritos = (await _db.ParticipantesSessaoTreinamento.Where(p => turmaIds.Contains(p.SessaoTreinamentoId) && p.Ativo)
            .Select(p => p.SessaoTreinamentoId).ToListAsync(ct)).GroupBy(i => i).ToDictionary(g => g.Key, g => g.Count());
        var obras = await _db.Obras.Select(o => new { o.Id, o.Nome }).ToDictionaryAsync(o => o.Id, o => o.Nome, ct);
        return turmas.OrderBy(t => t.DataRealizacao)
            .Select(t => new TurmaIntegracaoAgendadaDto(
                t.Id, t.ObraId, obras.GetValueOrDefault(t.ObraId, string.Empty), doCombo[t.CursoTreinamentoId], t.DataRealizacao,
                "agendada", inscritos.GetValueOrDefault(t.Id)))
            .ToList();
    }
}

// Lista os atendimentos para a aba Treinamentos › Integração. Escopo: filtro global por obra (obras do usuário) e, com o Entra
// ligado, só as obras em que ele é Técnico de Segurança/Administrador (ou todas, no vínculo global).
public record ListarIntegracoesQuery(Guid? ObraId = null, string? Status = null, string? AzureAdObjectId = null, bool AutenticacaoHabilitada = false)
    : IRequest<List<IntegracaoResumoDto>>;

public class ListarIntegracoesQueryHandler : IRequestHandler<ListarIntegracoesQuery, List<IntegracaoResumoDto>>
{
    private readonly IAppDbContext _db;

    public ListarIntegracoesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<IntegracaoResumoDto>> Handle(ListarIntegracoesQuery request, CancellationToken ct)
    {
        var consulta = _db.IntegracoesAtendimento.AsQueryable();
        if (request.ObraId is { } obraId)
        {
            await AutorizacaoIntegracaoAtendimento.AutorizarAsync(_db, obraId, request.AzureAdObjectId, request.AutenticacaoHabilitada, ct);
            consulta = consulta.Where(a => a.ObraId == obraId);
        }
        else if (request.AutenticacaoHabilitada)
        {
            var usuarioId = await _db.Usuarios
                .Where(u => u.AzureAdObjectId == request.AzureAdObjectId && u.Ativo && u.Status == StatusUsuario.Ativo)
                .Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);
            if (usuarioId is null) return new List<IntegracaoResumoDto>();
            var vinculos = await _db.UsuariosPerfilObra
                .Where(v => v.Ativo && v.UsuarioId == usuarioId && v.PerfilAcesso != null && v.PerfilAcesso.Ativo
                    && (v.PerfilAcesso.Tipo == TipoPerfilAcesso.TecnicoSeguranca || v.PerfilAcesso.Tipo == TipoPerfilAcesso.Administrador))
                .Select(v => v.ObraId).ToListAsync(ct);
            if (vinculos.Count == 0) return new List<IntegracaoResumoDto>();
            if (!vinculos.Contains(null))
            {
                var obras = vinculos.Select(o => o!.Value).ToList();
                consulta = consulta.Where(a => obras.Contains(a.ObraId));
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<StatusIntegracaoAtendimento>(request.Status, ignoreCase: true, out var status))
                throw new InvalidOperationException("Status inválido. Use emAndamento, concluido ou cancelado.");
            consulta = consulta.Where(a => a.Status == status);
        }

        var atendimentos = await consulta.OrderByDescending(a => a.IniciadaEmUtc).Take(200).ToListAsync(ct);
        var ids = atendimentos.Select(a => a.Id).ToList();
        var obrasIds = atendimentos.Select(a => a.ObraId).Distinct().ToList();
        var obraNomes = await _db.Obras.Where(o => obrasIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Nome, ct);
        var contagem = (await _db.ParticipantesIntegracao.Where(p => ids.Contains(p.IntegracaoAtendimentoId))
            .Select(p => p.IntegracaoAtendimentoId).ToListAsync(ct)).GroupBy(i => i).ToDictionary(g => g.Key, g => g.Count());
        var usuariosIds = atendimentos.Where(a => a.ResponsavelUsuarioId != null).Select(a => a.ResponsavelUsuarioId!.Value).Distinct().ToList();
        var responsaveis = await _db.Usuarios.IgnoreQueryFilters().Where(u => usuariosIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Nome, ct);
        var tipos = (await _db.IntegracaoEtapas.Where(e => ids.Contains(e.IntegracaoAtendimentoId))
            .Select(e => new { e.IntegracaoAtendimentoId, e.Ordem, e.Tipo }).ToListAsync(ct))
            .ToDictionary(e => (e.IntegracaoAtendimentoId, e.Ordem), e => e.Tipo);

        return atendimentos.Select(a => new IntegracaoResumoDto(
            a.Id, a.Numero, a.ObraId, obraNomes.GetValueOrDefault(a.ObraId, string.Empty), TextoDto.Camel(a.Status.ToString()), a.EtapaAtualOrdem,
            a.EtapaAtualOrdem is { } ordem && tipos.TryGetValue((a.Id, ordem), out var tipo) ? CalculadoraEtapasIntegracao.Titulo(tipo) : null,
            contagem.GetValueOrDefault(a.Id), a.IniciadaEmUtc, a.ConcluidaEmUtc,
            a.ResponsavelUsuarioId is { } r ? responsaveis.GetValueOrDefault(r) : null)).ToList();
    }
}
