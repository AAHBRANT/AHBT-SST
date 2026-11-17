using System.Text.Json;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Integracoes;
using AAHBRANT.SST.Application.IntegracoesAtendimento;
using AAHBRANT.SST.Application.IntegracoesAtendimento.Commands;
using AAHBRANT.SST.Application.IntegracoesAtendimento.Queries;
using AAHBRANT.SST.Application.OrdensServico;
using AAHBRANT.SST.Application.PlanosIntegracao.Queries;
using AAHBRANT.SST.Application.SessoesTreinamento.Commands;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Application.Tests.IntegracoesAtendimento;

internal sealed class NumerosFalsos : IGeradorNumeroDocumentoService
{
    private int _n;
    public Task<string> GerarAsync(string prefixo, CancellationToken ct) => Task.FromResult($"{prefixo}-2026-{Interlocked.Increment(ref _n):0000}");
}

internal sealed class AuditoriaIntegracaoFalsa : IAuditoriaService
{
    public Task RegistrarAsync(string acao, string entidadeTipo, Guid entidadeId, Guid? usuarioId, Guid? trabalhadorId, object? dadosDepois, CancellationToken ct)
        => Task.CompletedTask;
}

internal sealed class ClienteIntegracaoFalso : IClienteIpProvider
{
    public string? ObterIp() => "10.0.0.9";
    public string? ObterUserAgent() => "Teste";
}

// Leitor de digital falso: devolve como identificado o trabalhador informado (ou o que a função escolher).
internal sealed class LeitorDigitalFalso(Func<Guid, Guid>? quemELido = null) : IAutenticacaoBiometriaLocalService
{
    public bool PermitiuOutraObra { get; private set; }

    public Task<ResultadoAutenticacaoAssinatura> AutenticarPorMatchLocalAsync(
        Guid dispositivoId, string segredoDispositivo, Guid trabalhadorId, double score, LeituraBiometricaAssinada? leitura, CancellationToken ct,
        bool permitirInstrutorDeOutraObra = false)
    {
        PermitiuOutraObra = permitirInstrutorDeOutraObra;
        return Task.FromResult(new ResultadoAutenticacaoAssinatura(
            quemELido?.Invoke(trabalhadorId) ?? trabalhadorId, MetodoAutenticacaoAssinatura.Biometria, dispositivoId, Confianca: score));
    }
}

// Reconhecimento facial falso: identifica a pessoa configurada e guarda em qual obra (grupo) a busca foi feita.
internal sealed class FacialIntegracaoFalso(Func<Guid>? quem = null, MotivoRejeicaoFacial? rejeitar = null) : IAutenticacaoFacialService
{
    public Guid? ObraConsultada { get; private set; }

    public Task<ResultadoIdentificacaoFacial> IdentificarAsync(Guid obraId, byte[] fotoJpeg, CancellationToken ct, bool exigirMargemSobreSegundoColocado = true)
    {
        ObraConsultada = obraId;
        if (rejeitar is { } motivo) return Task.FromResult(new ResultadoIdentificacaoFacial(false, null, motivo, null));
        return Task.FromResult(new ResultadoIdentificacaoFacial(true,
            new ResultadoAutenticacaoAssinatura(quem!(), MetodoAutenticacaoAssinatura.ReconhecimentoFacial, null, "recognition_04", "grupo-obra", "req-1", 91), null, 91));
    }

    public Task<DiagnosticoVinculosFacialDto> DiagnosticarVinculosAsync(Guid obraId, CancellationToken ct) => throw new NotSupportedException();
    public Task CadastrarAsync(Guid trabalhadorId, byte[] fotoJpeg, CancellationToken ct) => throw new NotSupportedException();
    public Task RemoverCadastroAsync(Guid trabalhadorId, CancellationToken ct) => throw new NotSupportedException();
    public Task RealocarCadastroAsync(Guid trabalhadorId, Guid obraAnteriorId, CancellationToken ct) => throw new NotSupportedException();
    public Task<ResultadoMigracaoModeloFacial?> MigrarModeloReconhecimentoAsync(Guid obraId, CancellationToken ct) => throw new NotSupportedException();
}

// IMediator controlado pelo teste, com os dois formatos de Send (com e sem resposta).
internal sealed class RoteadorMediator(Func<object, CancellationToken, Task<object?>> responder) : IMediator
{
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
        ResponderAsync<TResponse>(request, cancellationToken);

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest =>
        responder(request, cancellationToken);

    private async Task<TResponse> ResponderAsync<TResponse>(object request, CancellationToken ct) => (TResponse)(await responder(request, ct))!;

    public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task Publish(object notification, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => throw new NotSupportedException();
}

// Obra com instrutor, cursos das 4 NRs e duas pessoas: A (Operador: NR-12 e NR-35 pelo PGR) e B (Ajudante: só NR-06 e NR-18).
internal sealed class CenarioIntegracao
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public required SstDbContext Db { get; init; }
    public required DbContextOptions<SstDbContext> OpcoesDb { get; init; }
    public required Obra Obra { get; init; }
    public required Funcao Operador { get; init; }
    public required Funcao Ajudante { get; init; }
    public required Trabalhador Instrutor { get; init; }
    public required Trabalhador A { get; init; }
    public required Trabalhador B { get; init; }
    public required CursoTreinamento Nr06 { get; init; }
    public required CursoTreinamento Nr12 { get; init; }
    public required CursoTreinamento Nr18 { get; init; }
    public required CursoTreinamento Nr35 { get; init; }
    public required CursoTreinamento CursoIntegracao { get; init; }

    public OpcoesIntegracao Opcoes { get; } = new() { AtendimentoHabilitado = true };
    public NumerosFalsos Numeros { get; } = new();
    public Func<object, CancellationToken, Task<object?>>? RespostasExtras { get; set; }

    // Assinatura biométrica do instrutor guardada na turma (o que as rotas instrutor/assinar/* gravam).
    public async Task AssinarInstrutorAsync(Guid sessaoId, MetodoAutenticacaoAssinatura metodo = MetodoAutenticacaoAssinatura.Biometria)
    {
        Db.AssinaturasInstrutorSessaoTreinamento.Add(new AssinaturaInstrutorSessaoTreinamento
        {
            ObraId = Obra.Id, SessaoTreinamentoId = sessaoId, InstrutorTrabalhadorId = Instrutor.Id, Metodo = metodo,
            AssinadoEmUtc = DateTime.UtcNow, ScoreConfianca = 88, EvidenciaConteudo = [9, 8, 7], EvidenciaContentType = "image/png",
            EvidenciaHash = "HASH-INSTRUTOR", IpAddress = "10.0.0.5", UserAgent = "Tablet da obra",
            LocalizacaoStatus = StatusLocalizacaoAssinatura.Capturada, Latitude = -7.1, Longitude = -34.8, PrecisaoMetros = 12,
            DispositivoAgenteId = Guid.NewGuid(),
        });
        await Db.SaveChangesAsync();
    }

    public static async Task<CenarioIntegracao> CriarAsync()
    {
        ChavesCpfDeTeste.Configurar();
        var opcoesDb = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new SstDbContext(opcoesDb, new CurrentUserService());

        var obra = new Obra { Codigo = "INT", Nome = "Obra da Integração" };
        var operador = new Funcao { Nome = "Operador de Máquinas" };
        var ajudante = new Funcao { Nome = "Ajudante Geral" };
        var nr06 = new CursoTreinamento { Nome = "NR-06 Uso de EPI", NormaReferencia = "NR-06", AtendeNr6 = true, ValidadeEmMeses = 12 };
        var nr12 = new CursoTreinamento { Nome = "NR-12 Máquinas e Equipamentos", NormaReferencia = "NR-12", ValidadeEmMeses = 24 };
        var nr18 = new CursoTreinamento { Nome = "NR-18 Construção Civil", NormaReferencia = "NR-18", CargaHorariaMinima = 4, ValidadeEmMeses = 24 };
        var nr35 = new CursoTreinamento { Nome = "NR-35 Trabalho em Altura", NormaReferencia = "NR-35", CargaHorariaMinima = 8, ValidadeEmMeses = 24 };
        var integracao = new CursoTreinamento { Nome = "Integração de Segurança", EhIntegracaoSeguranca = true, ValidadeEmMeses = 12 };
        db.Obras.Add(obra);
        db.Funcoes.AddRange(operador, ajudante);
        db.CursosTreinamento.AddRange(nr06, nr12, nr18, nr35, integracao);
        // Sem PGR aprovado na obra, vale a matriz da função (reserva): o Operador exige NR-12 e NR-35.
        db.MatrizTreinamentoFuncoes.AddRange(
            new MatrizTreinamentoFuncao { FuncaoId = operador.Id, CursoTreinamentoId = nr12.Id },
            new MatrizTreinamentoFuncao { FuncaoId = operador.Id, CursoTreinamentoId = nr35.Id });

        var instrutor = Pessoa(obra, ajudante, "Ivo Instrutor", "11111111111");
        var a = Pessoa(obra, operador, "Ana Operadora", "22222222222");
        var b = Pessoa(obra, ajudante, "Beto Ajudante", "33333333333");
        db.Trabalhadores.AddRange(instrutor, a, b);
        db.PerfisInstrutor.Add(new PerfilInstrutor { TrabalhadorId = instrutor.Id });
        // O instrutor precisa de digital (ou facial) cadastrada para assinar e para a integração iniciar (16/11/2026).
        db.TemplatesBiometricoFutronic.Add(new TemplateBiometricoFutronic { TrabalhadorId = instrutor.Id, TemplateCriptografado = "x", CapturadoEm = DateTime.UtcNow });
        await db.SaveChangesAsync();

        return new CenarioIntegracao
        {
            Db = db, OpcoesDb = opcoesDb, Obra = obra, Operador = operador, Ajudante = ajudante, Instrutor = instrutor, A = a, B = b,
            Nr06 = nr06, Nr12 = nr12, Nr18 = nr18, Nr35 = nr35, CursoIntegracao = integracao,
        };
    }

    public static Trabalhador Pessoa(Obra obra, Funcao funcao, string nome, string cpf) =>
        new() { ObraId = obra.Id, FuncaoId = funcao.Id, Nome = nome, Cpf = cpf, DataAdmissao = DateTime.UtcNow.Date };

    public async Task<Trabalhador> NovaPessoaAsync(Funcao funcao, string nome)
    {
        var t = Pessoa(Obra, funcao, nome, "9" + Random.Shared.NextInt64(10_000_000_00, 99_999_999_99));
        Db.Trabalhadores.Add(t);
        await Db.SaveChangesAsync();
        return t;
    }

    public async Task DarTreinamentoValidoAsync(Trabalhador t, params CursoTreinamento[] cursos)
    {
        foreach (var c in cursos)
            Db.Treinamentos.Add(new Treinamento
            {
                TrabalhadorId = t.Id, CursoTreinamentoId = c.Id, DataRealizacao = DateTime.UtcNow.AddMonths(-1),
                DataValidade = DateTime.UtcNow.AddYears(1), NumeroCertificado = "ANTIGO",
            });
        await Db.SaveChangesAsync();
    }

    public IniciarIntegracaoCommandHandler Iniciar() => new(Db, Numeros, Options.Create(Opcoes));

    public async Task<Guid> IniciarAsync(params Trabalhador[] pessoas) =>
        await Iniciar().Handle(new IniciarIntegracaoCommand(Obra.Id, pessoas.Select(p => p.Id).ToList(), Instrutor.Id), default);

    public RoteadorMediator Mediator => new(async (req, ct) =>
    {
        switch (req)
        {
            case EncerrarSessaoTreinamentoCommand c:
                await EncerrarHandler().Handle(c, ct);
                return null;
            case CriarDocumentoAssinaturaCommand c:
                var doc = new DocumentoAssinatura { EntidadeTipo = c.EntidadeTipo, EntidadeId = c.EntidadeId, Status = StatusDocumentoAssinatura.EmAndamento };
                Db.DocumentosAssinatura.Add(doc);
                await Db.SaveChangesAsync(ct);
                return doc.Id;
            case ListarOsIntegracaoQuery q:
                return await new ListarOsIntegracaoQueryHandler(Db, Mediator).Handle(q, ct);
            case ListarPlanosIntegracaoPendentesQuery:
                return new List<PlanoIntegracaoDto>();
        }
        if (RespostasExtras is not null) return await RespostasExtras(req, ct);
        throw new NotSupportedException(req.GetType().Name);
    });

    public AssinarInstrutorEtapaPorDigitalCommandHandler AssinarDigitalHandler(Func<Guid, Guid>? quemELido = null) =>
        new(Db, new LeitorDigitalFalso(quemELido), new ClienteIntegracaoFalso());

    public AssinarInstrutorEtapaPorFacialCommandHandler AssinarFacialHandler(FacialIntegracaoFalso facial) =>
        new(Db, facial, new ClienteIntegracaoFalso());

    public EncerrarSessaoTreinamentoCommandHandler EncerrarHandler() => new(
        Db, Mediator, new RegistradorAssinaturaService(Db, new AuditoriaIntegracaoFalsa(), new ClienteIntegracaoFalso()),
        NullLogger<EncerrarSessaoTreinamentoCommandHandler>.Instance, Numeros);

    public AvancarEtapaIntegracaoCommandHandler Avancar() => new(Db, Mediator, Numeros);

    public ConcluirIntegracaoCommandHandler Concluir() => new(Db, Mediator);

    public Task<IntegracaoAtendimentoDto> AvancarAsync(Guid id, int? ordem = null) =>
        Avancar().Handle(new AvancarEtapaIntegracaoCommand(id, ordem), default);

    public async Task<IntegracaoAtendimentoDto> ObterAsync(Guid id) =>
        (await new ObterIntegracaoQueryHandler(Db).Handle(new ObterIntegracaoQuery(id), default))!;

    public async Task<IntegracaoEtapa> EtapaAsync(Guid atendimentoId, int ordem) =>
        await Db.IntegracaoEtapas.AsNoTracking().SingleAsync(e => e.IntegracaoAtendimentoId == atendimentoId && e.Ordem == ordem);

    // Metadados de captura de uma foto da câmera; "capturadaEm" é o horário informado pelo aparelho.
    public string Metadados(Guid sessaoId, DateTimeOffset? capturadaEm = null, string origem = "camera")
    {
        var quando = capturadaEm ?? DateTimeOffset.UtcNow.AddSeconds(1);
        return JsonSerializer.Serialize(new DadosCapturaFoto
        {
            CapturadaEm = quando, Origem = origem, RegistroId = sessaoId, ObraId = Obra.Id,
            Latitude = -7.1, Longitude = -34.8, PrecisaoMetros = 10, LocalizacaoObtidaEm = quando,
        }, Web);
    }

    public async Task<string> TokenAsync(Guid sessaoId) =>
        (await new EmitirTokenCapturaFotoCommandHandler(Db).Handle(new EmitirTokenCapturaFotoCommand(sessaoId), default)).Token;

    // Turma de integração: emite o token de captura (uso único) antes de anexar; "token" informado substitui o emitido.
    public async Task<Guid> FotoAsync(Guid sessaoId, int ordem, byte[] bytes, DateTimeOffset? capturadaEm = null, string origem = "camera", string? token = null, bool semToken = false)
    {
        var deIntegracao = await Db.SessoesTreinamento.AnyAsync(s => s.Id == sessaoId && s.IntegracaoAtendimentoId != null);
        if (token is null && !semToken && deIntegracao) token = await TokenAsync(sessaoId);
        return await new AnexarFotoEvidenciaSessaoTreinamentoCommandHandler(Db).Handle(
            new AnexarFotoEvidenciaSessaoTreinamentoCommand(sessaoId, ordem, bytes, "image/jpeg", Metadados(sessaoId, capturadaEm, origem), token), default);
    }

    // Põe o início da etapa no passado (os períodos só valem dentro do dia da etapa e antes de agora) e devolve a data base (hoje - 5).
    public async Task<string> RecuarEtapaAsync(Guid etapaId, int dias = 10)
    {
        var etapa = await Db.IntegracaoEtapas.SingleAsync(e => e.Id == etapaId);
        etapa.IniciadaEmUtc = DateTime.UtcNow.AddDays(-dias);
        // O período automático (lançado na abertura) sai: o técnico "ajusta o horário" removendo e lançando à mão.
        Db.PeriodosEtapaIntegracao.RemoveRange(await Db.PeriodosEtapaIntegracao.Where(p => p.IntegracaoEtapaId == etapaId && p.Automatico).ToListAsync());
        await Db.SaveChangesAsync();
        return DataDaEtapa(etapa.Ordem);
    }

    public static string DataDaEtapa(int ordem) =>
        ExigenciasEtapasIntegracao.HojeBrasilia().AddDays(-9 + ordem).ToString("yyyy-MM-dd");

    public static byte[] Bytes(int semente) => [(byte)(semente % 250 + 1), (byte)(semente / 250 + 1), 7, 9, 11];

    // Deixa a etapa atual pronta para avançar: presença de todos, 3 fotos próprias e a carga mínima lançada.
    public async Task CompletarEtapaAtualAsync(Guid atendimentoId, int semente, bool comPeriodo = true, bool comInstrutor = true)
    {
        var at = await Db.IntegracoesAtendimento.SingleAsync(a => a.Id == atendimentoId);
        var etapa = await Db.IntegracaoEtapas.SingleAsync(e => e.IntegracaoAtendimentoId == atendimentoId && e.Ordem == at.EtapaAtualOrdem);
        var sessaoId = etapa.SessaoTreinamentoId!.Value;
        var dataEtapa = await RecuarEtapaAsync(etapa.Id);

        foreach (var p in await Db.ParticipantesSessaoTreinamento.Where(p => p.SessaoTreinamentoId == sessaoId).ToListAsync())
        {
            p.PresencaConfirmadaEm = DateTime.UtcNow;
            p.MetodoPresenca = MetodoAutenticacaoAssinatura.Biometria;
            p.EvidenciaConteudo = [1, 2, 3];
            p.EvidenciaContentType = "image/png";
        }
        await Db.SaveChangesAsync();
        if (comInstrutor && !await Db.AssinaturasInstrutorSessaoTreinamento.AnyAsync(a => a.SessaoTreinamentoId == sessaoId))
            await AssinarInstrutorAsync(sessaoId);

        for (var ordem = 1; ordem <= 3; ordem++)
            await FotoAsync(sessaoId, ordem, Bytes(semente * 10 + ordem));

        if (comPeriodo)
        {
            var fim = TimeSpan.FromHours(8 + Math.Max(1, etapa.CargaMinimaHoras));
            await new RegistrarPeriodoEtapaIntegracaoCommandHandler(Db).Handle(new RegistrarPeriodoEtapaIntegracaoCommand(
                atendimentoId, etapa.Id, dataEtapa, "08:00", fim.ToString(@"hh\:mm")), default);
        }
    }

    // OS emitida e assinada (funcionário + técnico) depois do início do atendimento.
    public async Task<OrdemServico> OsAssinadaAsync(Guid trabalhadorId, bool assinarTecnico = true)
    {
        var os = new OrdemServico
        {
            TrabalhadorId = trabalhadorId, ObraId = Obra.Id, Numero = $"OS-{Guid.NewGuid():N}"[..12], Revisao = 0,
            EmitidaEm = DateTime.UtcNow, ConteudoJson = "{}",
        };
        var doc = new DocumentoAssinatura { EntidadeTipo = OrdemServico.EntidadeTipoAssinatura, EntidadeId = os.Id, ObraId = Obra.Id };
        Db.OrdensServico.Add(os);
        Db.DocumentosAssinatura.Add(doc);
        Db.DocumentoSignatarios.Add(new DocumentoSignatario
        {
            DocumentoAssinaturaId = doc.Id, TrabalhadorId = trabalhadorId, MetodoAutenticacao = MetodoAutenticacaoAssinatura.Biometria,
            AssinadoEm = DateTime.UtcNow.AddMinutes(1),
        });
        if (assinarTecnico)
            Db.DocumentoSignatarios.Add(new DocumentoSignatario
            {
                DocumentoAssinaturaId = doc.Id, TrabalhadorId = Instrutor.Id, MetodoAutenticacao = MetodoAutenticacaoAssinatura.SessaoLogada,
                AssinadoEm = DateTime.UtcNow.AddMinutes(2),
            });
        await Db.SaveChangesAsync();
        return os;
    }

    public static MinutaOrdemServicoDto Minuta(Guid trabalhadorId, params string[] tiposDePendencia) => new()
    {
        TrabalhadorId = trabalhadorId,
        Pendencias = tiposDePendencia
            .Select(t => new PendenciaOsDto(t, t == "SemAso" ? "Trabalhador sem ASO" : t, "motivo",
                t is "SemAso" or "AsoInvalido" or "TreinamentoPendente" ? "RH" : "SST", "aso", null)).ToList(),
    };

    // Todos já têm as 4 NRs: as etapas de curso viram Puladas e a integração abre direto na OS.
    public async Task<Guid> IniciarDiretoNaOsAsync(params Trabalhador[] pessoas)
    {
        foreach (var p in pessoas)
            await DarTreinamentoValidoAsync(p, Nr06, Nr12, Nr18, Nr35);
        return await IniciarAsync(pessoas);
    }
}
