using AAHBRANT.SST.Application.Acidentes;
using AAHBRANT.SST.Application.Acidentes.RelatoIa;
using AAHBRANT.SST.Application.AcoesPlano;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Acidentes.Commands;

public record CriarAcidenteCommand(
    TipoOcorrencia Tipo,
    Guid ObraId,
    Guid? TrabalhadorId,
    Guid? AtividadeId,
    string Local,
    DateTime Data,
    TimeSpan? Hora,
    string Descricao,
    string? Lesao,
    string? Consequencia,
    string? Atendimento,
    bool HouveAfastamento,
    int? DiasAfastamento,
    string? NumeroCat,
    MetodologiaInvestigacao? MetodologiaInvestigacao,
    string? Causas,
    GravidadeAcidente Gravidade,
    int? DiasDebitadosInformados,
    List<Guid>? TrabalhadoresIds = null,
    // Plano de ação revisado pelo técnico no registro por relato (IA). Criado junto com o acidente.
    List<AcaoPlanoNovaOcorrencia>? AcoesPlano = null,
    // Tema obrigatório do DDS do próximo dia útil (registro por relato).
    TemaDdsNovaOcorrencia? TemaDds = null,
    // Preenchido pelo controller a partir do token (nunca do corpo): responsável pela ação de DDS.
    Guid? UsuarioAtualId = null,
    // Acidente/Doença ocupacional por relato: reunião de análise obrigatória no Teams.
    ReuniaoAnaliseNovaOcorrencia? Reuniao = null) : IRequest<Guid>;

public record TemaDdsNovaOcorrencia(string Nome, string Roteiro);

public record ReuniaoAnaliseNovaOcorrencia(DateTime Inicio, int DuracaoMinutos, List<Guid> ParticipantesUsuarioIds);

public record AcaoPlanoNovaOcorrencia(
    TipoAcaoPlano Tipo,
    string Descricao,
    PrioridadeAcao Prioridade,
    DateTime? Prazo,
    Guid? ResponsavelUsuarioId,
    string? Fundamentacao);

public class CriarAcidenteCommandValidator : AbstractValidator<CriarAcidenteCommand>
{
    public CriarAcidenteCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Local).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Lesao).MaximumLength(500);
        RuleFor(x => x.Consequencia).MaximumLength(500);
        RuleFor(x => x.Atendimento).MaximumLength(500);
        RuleFor(x => x.NumeroCat).MaximumLength(50);
        RuleFor(x => x.Causas).MaximumLength(2000);
        RuleFor(x => x.DiasAfastamento).GreaterThanOrEqualTo(0).When(x => x.DiasAfastamento.HasValue);
        RuleFor(x => x.DiasDebitadosInformados)
            .NotNull().WithMessage("Informe os Dias Debitados consultando o Quadro III da NBR 14280.")
            .GreaterThan(0)
            .When(x => x.Gravidade == GravidadeAcidente.IncapacidadePermanenteParcial);
        // Registro por relato (veio plano de ação): o tema do DDS do dia seguinte é obrigatório.
        RuleFor(x => x.TemaDds)
            .NotNull().WithMessage("Informe o tema do DDS do dia seguinte.")
            .When(x => x.AcoesPlano is not null);
        RuleFor(x => x.TemaDds!.Nome).NotEmpty().WithMessage("Informe o tema do DDS do dia seguinte.").MaximumLength(200)
            .When(x => x.TemaDds is not null);
        RuleFor(x => x.TemaDds!.Roteiro).NotEmpty().WithMessage("Informe o roteiro do DDS do dia seguinte.").MaximumLength(1000)
            .When(x => x.TemaDds is not null);
        RuleFor(x => x.Reuniao)
            .NotNull().WithMessage("Informe a data e o horário da reunião de análise do acidente.")
            .When(x => x.AcoesPlano is not null && (x.Tipo is TipoOcorrencia.Acidente or TipoOcorrencia.DoencaOcupacional));
        RuleFor(x => x.Reuniao!.DuracaoMinutos).InclusiveBetween(15, 240).When(x => x.Reuniao is not null);
        RuleForEach(x => x.AcoesPlano).ChildRules(a =>
        {
            a.RuleFor(x => x.Descricao).NotEmpty().MaximumLength(500);
            a.RuleFor(x => x.Fundamentacao).MaximumLength(500);
        });
    }
}

public class CriarAcidenteCommandHandler : IRequestHandler<CriarAcidenteCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IPublicadorAcidenteGrh _publicadorGrh;
    private readonly IReuniaoTeamsService? _reuniaoTeams;

    public CriarAcidenteCommandHandler(IAppDbContext db, IPublicadorAcidenteGrh publicadorGrh, IReuniaoTeamsService? reuniaoTeams = null)
    {
        _db = db;
        _publicadorGrh = publicadorGrh;
        _reuniaoTeams = reuniaoTeams;
    }

    public async Task<Guid> Handle(CriarAcidenteCommand request, CancellationToken ct)
    {
        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var envolvidos = AcidenteEnvolvidosSync.Resolver(request.TrabalhadoresIds, request.TrabalhadorId);
        await AcidenteEnvolvidosSync.ValidarAsync(_db, envolvidos, ct);

        if (request.AtividadeId.HasValue &&
            !await _db.Atividades.AnyAsync(a => a.Id == request.AtividadeId, ct))
            throw new KeyNotFoundException($"Atividade {request.AtividadeId} não encontrada.");

        var acidente = new Acidente
        {
            Tipo = request.Tipo,
            ObraId = request.ObraId,
            AtividadeId = request.AtividadeId,
            Local = request.Local,
            Data = request.Data,
            Hora = request.Hora,
            Descricao = request.Descricao,
            Lesao = request.Lesao,
            Consequencia = request.Consequencia,
            Atendimento = request.Atendimento,
            HouveAfastamento = request.HouveAfastamento,
            DiasAfastamento = request.DiasAfastamento,
            NumeroCat = request.NumeroCat,
            MetodologiaInvestigacao = request.MetodologiaInvestigacao,
            Causas = request.Causas,
            Gravidade = request.Gravidade,
            DiasDebitados = TabelaDiasDebitados.Calcular(request.Gravidade, request.DiasDebitadosInformados),
        };

        _db.Acidentes.Add(acidente);
        await AcidenteEnvolvidosSync.SincronizarAsync(_db, acidente, envolvidos, ct);

        foreach (var acao in request.AcoesPlano ?? new List<AcaoPlanoNovaOcorrencia>())
        {
            _db.AcoesPlano.Add(new AcaoPlano
            {
                OrigemTipo = nameof(Acidente),
                OrigemId = acidente.Id,
                Tipo = acao.Tipo,
                Descricao = acao.Descricao.Trim(),
                Prioridade = acao.Prioridade,
                Prazo = acao.Prazo ?? SlaPrioridadeCalculator.CalcularPrazoSugerido(acao.Prioridade, DateTime.UtcNow),
                ResponsavelUsuarioId = acao.ResponsavelUsuarioId,
                Fundamentacao = string.IsNullOrWhiteSpace(acao.Fundamentacao) ? null : acao.Fundamentacao.Trim(),
            });
        }

        if (request.TemaDds is not null)
            AgendarTemaDds(acidente, request.TemaDds, request.UsuarioAtualId);

        (ReuniaoAnaliseOcorrencia Entidade, List<string> Emails)? reuniao = request.Reuniao is null
            ? null
            : await RegistrarReuniaoAsync(acidente, request.Reuniao, request.UsuarioAtualId, ct);
        await _db.SaveChangesAsync(ct);
        await _publicadorGrh.PublicarAsync(await AcidenteGrhEventoFactory.CriarAsync(_db, acidente, ct), ct);
        if (reuniao is not null)
            await MarcarReuniaoNoTeamsAsync(acidente, reuniao.Value.Entidade, reuniao.Value.Emails, ct);
        return acidente.Id;
    }

    // O tema vira item do catálogo (fica disponível para outras obras também), ação do plano e
    // agendamento no DDS do próximo dia útil desta obra. A ação é concluída quando o DDS daquele dia
    // é encerrado com o tema (EncerrarDdsCommand).
    private void AgendarTemaDds(Acidente acidente, TemaDdsNovaOcorrencia tema, Guid? usuarioAtualId)
    {
        var dia = CalendarioObra.ProximoDiaUtil(CalendarioObra.AgoraEmBrasilia());
        var catalogo = new CatalogoTemaDds { Nome = tema.Nome.Trim(), Descricao = tema.Roteiro.Trim() };
        var acao = new AcaoPlano
        {
            OrigemTipo = nameof(Acidente),
            OrigemId = acidente.Id,
            Tipo = TipoAcaoPlano.Preventiva,
            Prioridade = PrioridadeAcao.Alta,
            Descricao = $"Realizar o DDS \"{catalogo.Nome}\" com a equipe da obra.",
            Prazo = dia,
            ResponsavelUsuarioId = usuarioAtualId,
            Fundamentacao = "DDS do dia seguinte · tema gerado a partir da ocorrência (obrigatório)",
        };
        _db.CatalogosTemaDds.Add(catalogo);
        _db.AcoesPlano.Add(acao);
        _db.TemasDdsAgendados.Add(new TemaDdsAgendado
        {
            ObraId = acidente.ObraId,
            Data = dia,
            CatalogoTemaDdsId = catalogo.Id,
            OrigemTipo = nameof(Acidente),
            OrigemId = acidente.Id,
            DescricaoOrigem = $"ocorrência de {acidente.Data:dd/MM} · {acidente.Local}",
            AcaoPlanoId = acao.Id,
        });
    }

    // A reunião vira ação do plano e registro próprio; o Teams é chamado só depois do registro gravado.
    private async Task<(ReuniaoAnaliseOcorrencia Entidade, List<string> Emails)> RegistrarReuniaoAsync(
        Acidente acidente, ReuniaoAnaliseNovaOcorrencia dados, Guid? organizadorId, CancellationToken ct)
    {
        var ids = dados.ParticipantesUsuarioIds.Append(organizadorId ?? Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToList();
        var pessoas = await _db.Usuarios.Where(u => ids.Contains(u.Id)).Select(u => new { u.Id, u.Nome, u.Email }).ToListAsync(ct);

        var acao = new AcaoPlano
        {
            OrigemTipo = nameof(Acidente),
            OrigemId = acidente.Id,
            Tipo = TipoAcaoPlano.Corretiva,
            Prioridade = PrioridadeAcao.Alta,
            Descricao = "Realizar a reunião de análise do acidente para validar as causas e o plano de ação.",
            Prazo = dados.Inicio,
            ResponsavelUsuarioId = organizadorId,
            Fundamentacao = "Reunião de análise · obrigatória em todo acidente",
        };
        var reuniao = new ReuniaoAnaliseOcorrencia
        {
            AcidenteId = acidente.Id,
            AcaoPlanoId = acao.Id,
            Inicio = dados.Inicio,
            Fim = dados.Inicio.AddMinutes(dados.DuracaoMinutos),
            OrganizadorUsuarioId = organizadorId,
            Participantes = string.Join("\n", pessoas.OrderBy(p => p.Id == organizadorId ? 0 : 1).ThenBy(p => p.Nome).Select(p => p.Nome)),
        };
        _db.AcoesPlano.Add(acao);
        _db.ReunioesAnaliseOcorrencia.Add(reuniao);
        return (reuniao, pessoas.Select(p => p.Email).ToList());
    }

    private async Task MarcarReuniaoNoTeamsAsync(Acidente acidente, ReuniaoAnaliseOcorrencia reuniao, List<string> emails, CancellationToken ct)
    {
        if (reuniao.OrganizadorUsuarioId is null)
            reuniao.MotivoFalha = "Quem registrou não está vinculado a uma conta Microsoft. Marque a reunião no Teams manualmente.";
        else if (_reuniaoTeams is null || !_reuniaoTeams.Configurado)
            reuniao.MotivoFalha = "Integração com o Teams não configurada neste ambiente. Marque a reunião no Teams manualmente.";
        else
        {
            try
            {
                var criada = await _reuniaoTeams.CriarReuniaoAsync(
                    reuniao.OrganizadorUsuarioId.Value,
                    $"Análise de acidente · {acidente.Local} · {acidente.Data:dd/MM}",
                    $"Reunião de análise do acidente registrado em {acidente.Data:dd/MM/yyyy} ({acidente.Local}).\n\n" +
                    $"Pauta: validar as causas e o plano de ação registrados no sistema SST.\n\nO que aconteceu: {acidente.Descricao}",
                    reuniao.Inicio, reuniao.Fim, emails, ct);
                reuniao.GraphEventId = criada.GraphEventId;
                reuniao.LinkTeams = criada.LinkTeams;
                reuniao.Situacao = SituacaoReuniaoTeams.Criada;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // O registro do acidente já foi gravado; falha no Teams não pode desfazê-lo.
                reuniao.MotivoFalha = Limitar($"O Teams não aceitou a reunião ({ex.Message}). Marque-a manualmente.", 500);
            }
        }

        if (reuniao.Situacao != SituacaoReuniaoTeams.Criada)
            reuniao.Situacao = SituacaoReuniaoTeams.NaoCriada;
        await _db.SaveChangesAsync(ct);
    }

    private static string Limitar(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..maximo];
}
