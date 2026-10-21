using System.Globalization;
using System.Text;
using AAHBRANT.SST.Application.Alertas;
using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PlanosIntegracao;

public interface IPlanoIntegracaoService
{
    // motivo preenchido = evento que abre um plano novo (admissão, troca de função, transferência) e
    // avisa os técnicos. Nulo = só reajusta o kit (ex.: desligamento cancela as reservas).
    Task SincronizarAsync(Guid trabalhadorId, MotivoPlanoIntegracao? motivo, CancellationToken ct = default);
}

// Especificação de 21/10/2026, Módulo 1. Quando o funcionário chega ao SST ele já foi recrutado para
// a obra e a equipe — por isso o kit da função é separado sem pedir confirmação a ninguém.
public class PlanoIntegracaoService : IPlanoIntegracaoService
{
    // Carga do G-RH em obra nova traz gente admitida há anos: isso não é "funcionário novo" e não
    // deve gerar plano nem uma enxurrada de avisos. Os chamadores usam este corte.
    public static readonly TimeSpan JanelaAdmissaoRecente = TimeSpan.FromDays(30);

    private readonly IAppDbContext _db;
    private readonly ITecnicosSegurancaPorObraService _tecnicos;
    private readonly IFilaNotificacaoTeams _filaTeams;
    private readonly IFilaCalendarioTeams _filaCalendario;
    private readonly IGeradorNumeroDocumentoService _geradorNumero;

    private readonly EstoquesEpi.Minimo.IVerificacaoEstoqueMinimoService? _verificacaoMinimo;

    public PlanoIntegracaoService(IAppDbContext db, ITecnicosSegurancaPorObraService tecnicos, IFilaNotificacaoTeams filaTeams,
        IFilaCalendarioTeams filaCalendario, IGeradorNumeroDocumentoService geradorNumero,
        EstoquesEpi.Minimo.IVerificacaoEstoqueMinimoService? verificacaoMinimo = null)
    {
        _verificacaoMinimo = verificacaoMinimo;
        _db = db;
        _tecnicos = tecnicos;
        _filaTeams = filaTeams;
        _filaCalendario = filaCalendario;
        _geradorNumero = geradorNumero;
    }

    public static bool AdmissaoRecente(DateTime dataAdmissao) =>
        dataAdmissao.Date >= DateTime.UtcNow.Date - JanelaAdmissaoRecente;

    public async Task SincronizarAsync(Guid trabalhadorId, MotivoPlanoIntegracao? motivo, CancellationToken ct = default)
    {
        var trabalhador = await _db.Trabalhadores.IgnoreQueryFilters()
            .Include(t => t.Funcao)
            .FirstOrDefaultAsync(t => t.Id == trabalhadorId, ct);
        if (trabalhador is null) return;

        var agora = DateTime.UtcNow;
        var reservasAtivas = await _db.ReservasEpi
            .Where(r => r.TrabalhadorId == trabalhadorId && r.Status == StatusReservaEpi.Ativa)
            .ToListAsync(ct);
        var planosAbertos = await _db.PlanosIntegracao.IgnoreQueryFilters()
            .Where(p => p.TrabalhadorId == trabalhadorId && p.Ativo && p.Status == StatusPlanoIntegracao.Aberto)
            .ToListAsync(ct);

        var desligado = !trabalhador.Ativo
            || trabalhador.Situacao == SituacaoTrabalhador.Desligado
            || (trabalhador.DataDemissao is { } demissao && demissao.Date <= agora.Date);
        if (desligado)
        {
            foreach (var reserva in reservasAtivas) Cancelar(reserva, "Funcionário desligado.", agora);
            foreach (var plano in planosAbertos) Encerrar(plano, StatusPlanoIntegracao.Cancelado, agora);
            await _db.SaveChangesAsync(ct);
            if (_verificacaoMinimo is not null && reservasAtivas.Count > 0) await _verificacaoMinimo.VerificarAsync(trabalhador.ObraId, ct);
            return;
        }

        // Terceirizado já tem o kit reservado pelo cadastro na vaga do contrato (entrega pendente de
        // confirmação, ver CadastrarPessoaTerceirizadaCommand) — reservar de novo seria em dobro.
        var kitPeloContrato = trabalhador.Vinculo == TipoVinculo.Terceirizado;

        var episDaFuncao = await _db.MatrizEpiFuncoes
            .Where(m => m.FuncaoId == trabalhador.FuncaoId)
            .Select(m => new { m.CatalogoEpiId, Nome = m.CatalogoEpi!.Nome })
            .ToListAsync(ct);
        var idsDaFuncao = episDaFuncao.Select(e => e.CatalogoEpiId).ToHashSet();

        foreach (var reserva in reservasAtivas.Where(r => r.ObraId != trabalhador.ObraId || !idsDaFuncao.Contains(r.CatalogoEpiId) || kitPeloContrato))
        {
            var motivoCancelamento = reserva.ObraId != trabalhador.ObraId
                ? "Funcionário transferido de obra."
                : "EPI não faz mais parte da função do funcionário.";
            Cancelar(reserva, motivoCancelamento, agora);
        }

        if (!kitPeloContrato)
        {
            // Já está com o EPI (entrega não devolvida, confirmada ou não): não precisa separar.
            var emPosse = await _db.EntregasEpi
                .Where(e => e.TrabalhadorId == trabalhadorId && e.DataDevolucao == null)
                .Select(e => e.CatalogoEpiId)
                .Distinct()
                .ToListAsync(ct);
            var jaReservados = reservasAtivas
                .Where(r => r.Status == StatusReservaEpi.Ativa)
                .Select(r => r.CatalogoEpiId)
                .ToHashSet();

            foreach (var epi in episDaFuncao.Where(e => !emPosse.Contains(e.CatalogoEpiId) && !jaReservados.Contains(e.CatalogoEpiId)))
            {
                _db.ReservasEpi.Add(new ReservaEpi
                {
                    TrabalhadorId = trabalhadorId,
                    CatalogoEpiId = epi.CatalogoEpiId,
                    ObraId = trabalhador.ObraId,
                    Quantidade = 1,
                    Tamanho = TamanhoDoEpi(epi.Nome, trabalhador),
                });
            }
        }

        PlanoIntegracao? planoNovo = null;
        var turmas = new List<TurmaAgendada>();
        if (motivo is not null)
        {
            turmas = await AgendaTreinamentosIntegracao.AgendarAsync(_db, _geradorNumero, trabalhador, ct);
            foreach (var plano in planosAbertos) Encerrar(plano, StatusPlanoIntegracao.Cancelado, agora);
            planoNovo = new PlanoIntegracao
            {
                TrabalhadorId = trabalhadorId,
                ObraId = trabalhador.ObraId,
                FuncaoId = trabalhador.FuncaoId,
                Motivo = motivo.Value,
            };
            _db.PlanosIntegracao.Add(planoNovo);
        }

        await _db.SaveChangesAsync(ct);

        if (planoNovo is not null)
            await AvisarTecnicosAsync(trabalhador, planoNovo, turmas, ct);

        // Separar ou cancelar kit muda o disponível da obra (alerta de estoque mínimo, Módulo 2).
        if (_verificacaoMinimo is not null) await _verificacaoMinimo.VerificarAsync(trabalhador.ObraId, ct);
    }

    private async Task AvisarTecnicosAsync(Trabalhador trabalhador, PlanoIntegracao plano, List<TurmaAgendada> turmas, CancellationToken ct)
    {
        var tecnicos = await _tecnicos.ObterUsuarioIdsAsync(trabalhador.ObraId, ct);
        if (tecnicos.Count == 0) return;

        var reservas = await _db.ReservasEpi
            .Where(r => r.TrabalhadorId == trabalhador.Id && r.Status == StatusReservaEpi.Ativa)
            .Select(r => new { r.Id, r.CatalogoEpiId, Nome = r.CatalogoEpi!.Nome, r.Tamanho })
            .ToListAsync(ct);
        var separadas = await SituacaoReservasEpi.SeparadasAsync(
            _db, trabalhador.ObraId, reservas.Select(r => r.CatalogoEpiId).Distinct().ToList(), ct);
        var emFalta = reservas.Where(r => !separadas.GetValueOrDefault(r.Id))
            .Select(r => string.IsNullOrWhiteSpace(r.Tamanho) ? r.Nome : $"{r.Nome} (tam. {r.Tamanho})")
            .ToList();

        var funcao = trabalhador.Funcao?.Nome ?? "função não informada";
        var titulo = plano.Motivo switch
        {
            MotivoPlanoIntegracao.TrocaFuncao => $"Troca de função: {trabalhador.Nome} · {funcao}",
            MotivoPlanoIntegracao.TransferenciaObra => $"Funcionário transferido: {trabalhador.Nome} · {funcao}",
            _ => $"Funcionário novo: {trabalhador.Nome} · {funcao}",
        };
        var kit = reservas.Count == 0
            ? "Nenhum EPI a separar para a função."
            : emFalta.Count == 0
                ? $"Kit de EPI: {reservas.Count} item(ns) separado(s)."
                : $"Kit de EPI: {reservas.Count - emFalta.Count} separado(s), {emFalta.Count} em falta ({string.Join(", ", emFalta)}).";
        var treinamentos = turmas.Count == 0
            ? ""
            : $" Treinamentos agendados: {string.Join(", ", turmas.Select(t => $"{t.CursoNome} em {t.Data:dd/MM}"))}.";
        var descricao = $"{kit}{treinamentos} Abra o plano de integração para ver os próximos passos.";

        var alertas = tecnicos.Select(usuarioId => new Alerta
        {
            Tipo = TipoAlerta.IntegracaoFuncionarioNovo,
            Severidade = emFalta.Count > 0 ? SeveridadeAlerta.Critico : SeveridadeAlerta.Atencao,
            Titulo = titulo,
            Descricao = descricao,
            EntidadeOrigemTipo = nameof(PlanoIntegracao),
            EntidadeOrigemId = plano.Id,
            TrabalhadorId = trabalhador.Id,
            ObraId = trabalhador.ObraId,
            DestinatarioUsuarioId = usuarioId,
        }).ToList();
        _db.Alertas.AddRange(alertas);
        await _db.SaveChangesAsync(ct);

        // Envio proativo no Teams — enfileira e segue em frente (mesmo princípio de AlertaEngineService).
        foreach (var alerta in alertas)
            await _filaTeams.EnfileirarAsync(
                new NotificacaoTeamsMensagem(alerta.Id, alerta.DestinatarioUsuarioId!.Value, alerta.Titulo, alerta.Descricao), ct);

        // Evento no calendário do Teams de cada técnico, na data da primeira turma. Pertence ao alerta
        // (mesma origem "Alerta" do Motor de Alertas): resolver ou ignorar o alerta tira o evento.
        if (turmas.Count == 0) return;
        var primeiraTurma = turmas.Min(t => t.Data);
        foreach (var alerta in alertas)
            await _filaCalendario.EnfileirarAsync(new CalendarioTeamsMensagem(
                AlertaEngineService.OrigemCalendarioAlerta, alerta.Id, OperacaoCalendarioTeams.Criar,
                alerta.DestinatarioUsuarioId!.Value, $"Integração: {trabalhador.Nome} · {funcao}", alerta.Descricao, primeiraTurma), ct);
    }

    private static void Cancelar(ReservaEpi reserva, string motivo, DateTime agora)
    {
        reserva.Status = StatusReservaEpi.Cancelada;
        reserva.MotivoCancelamento = motivo;
        reserva.EncerradaEm = agora;
    }

    private static void Encerrar(PlanoIntegracao plano, StatusPlanoIntegracao status, DateTime agora)
    {
        plano.Status = status;
        plano.EncerradoEm = agora;
    }

    // O RH manda três tamanhos (blusa, calça, calçado); o catálogo de EPI não diz a que tipo cada item
    // pertence, então o tipo vem do nome. Só informativo: o estoque continua por EPI, sem tamanho.
    public static string? TamanhoDoEpi(string nomeEpi, Trabalhador trabalhador)
    {
        var nome = SemAcento(nomeEpi);
        if (ContemAlgum(nome, "BOTA", "BOTINA", "CALCADO", "SAPATO", "COTURNO"))
            return Limpo(trabalhador.TamanhoCalcadoEpi);
        if (ContemAlgum(nome, "CALCA"))
            return Limpo(trabalhador.TamanhoCalcaEpi);
        if (ContemAlgum(nome, "CAMISA", "BLUSA", "JAQUETA", "MACACAO", "JALECO", "CAPA DE CHUVA", "COLETE", "UNIFORME"))
            return Limpo(trabalhador.TamanhoBlusaEpi);
        return null;
    }

    private static string? Limpo(string? tamanho) => string.IsNullOrWhiteSpace(tamanho) ? null : tamanho.Trim();

    private static bool ContemAlgum(string texto, params string[] termos) => termos.Any(texto.Contains);

    private static string SemAcento(string texto)
    {
        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        foreach (var c in decomposto)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().ToUpperInvariant();
    }
}
