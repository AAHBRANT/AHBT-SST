using AAHBRANT.SST.Application.Funcoes;
using AAHBRANT.SST.Application.Integracoes;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AAHBRANT.SST.Application.Alertas;
using AAHBRANT.SST.Application.Alertas.Motor;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
    private readonly IOptions<OpcoesIntegracao>? _opcoes;

    // Origem dos avisos agrupados: um alerta por técnico, por obra e por dia (ver AvisarTecnicosAsync).
    public const string OrigemAvisoEntradasObra = "EntradasObra";

    private static readonly StatusAlerta[] StatusAlertaEmAberto =
        { StatusAlerta.Aberto, StatusAlerta.EmTratamento, StatusAlerta.Escalonado };

    public PlanoIntegracaoService(IAppDbContext db, ITecnicosSegurancaPorObraService tecnicos, IFilaNotificacaoTeams filaTeams,
        IFilaCalendarioTeams filaCalendario, IGeradorNumeroDocumentoService geradorNumero,
        EstoquesEpi.Minimo.IVerificacaoEstoqueMinimoService? verificacaoMinimo = null,
        IOptions<OpcoesIntegracao>? opcoes = null)
    {
        _verificacaoMinimo = verificacaoMinimo;
        _opcoes = opcoes;
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
        // Prestador PJ (04/11/2026) não é admitido na obra: sem kit de EPI nem plano de integração.
        if (trabalhador.Vinculo == TipoVinculo.PrestadorServico) return;

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

        var episDaFuncao = await _db.MatrizEpiFuncoes.ParaObra(trabalhador.ObraId)
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
        // Troca de função/transferência de quem já tem o pacote (NR-06, NR-18, NR-35) válido não precisa de
        // ficha nova: só o kit de EPI foi reajustado acima. Admissão sempre abre ficha.
        var dispensaFicha = motivo is MotivoPlanoIntegracao.TrocaFuncao or MotivoPlanoIntegracao.TransferenciaObra
            && (_opcoes?.Value.DispensarFichaComPacoteValido ?? new OpcoesIntegracao().DispensarFichaComPacoteValido)
            && await PacoteValidoAsync(trabalhadorId, ct);
        if (motivo is not null && !dispensaFicha)
        {
            await AgendaTreinamentosIntegracao.AgendarAsync(_db, _geradorNumero, trabalhador, ct);
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
            await AvisarTecnicosAsync(trabalhador, ct);

        // Separar ou cancelar kit muda o disponível da obra (alerta de estoque mínimo, Módulo 2).
        if (_verificacaoMinimo is not null) await _verificacaoMinimo.VerificarAsync(trabalhador.ObraId, ct);
    }

    // NR-06, NR-18 e NR-35 com treinamento dentro da validade (mesma noção de "válido" da agenda e da
    // liberação: DataValidade >= hoje, no dia de Brasília). NR-06 também vale pelo curso marcado AtendeNr6.
    private async Task<bool> PacoteValidoAsync(Guid trabalhadorId, CancellationToken ct)
    {
        var hoje = AgendaTreinamentosIntegracao.HojeBrasilia();
        var idsValidos = await _db.Treinamentos
            .Where(t => t.TrabalhadorId == trabalhadorId && t.DataValidade.Date >= hoje)
            .Select(t => t.CursoTreinamentoId)
            .Distinct()
            .ToListAsync(ct);
        if (idsValidos.Count == 0) return false;

        var cursosValidos = await _db.CursosTreinamento.Where(c => idsValidos.Contains(c.Id)).ToListAsync(ct);
        var normasValidas = cursosValidos.Select(PacoteIntegracao.NormaDoCurso).Where(n => n is not null).ToHashSet();
        return PacoteIntegracao.NormasObrigatorias.All(normasValidas.Contains);
    }

    // Aviso por obra (e não por pessoa): UM alerta por técnico, por obra e por dia. O mesmo alerta é
    // ATUALIZADO a cada ficha nova do dia — título com o contador, descrição recalculada a partir das
    // fichas do dia — e só a criação dele manda mensagem ao Teams. Não há evento de calendário: as turmas
    // do pacote são do fluxo "Realizar integração" e as demais vão listadas na descrição.
    //
    // Chave de dedup: o Motor de Alertas carrega os alertas em aberto num dicionário por
    // (EntidadeOrigemTipo, EntidadeOrigemId) e QUEBRA se houver chave repetida — por isso o
    // EntidadeOrigemId é um Guid determinístico de obra + técnico + dia (a obra fica também em ObraId).
    private async Task AvisarTecnicosAsync(Trabalhador trabalhador, CancellationToken ct)
    {
        var tecnicos = await _tecnicos.ObterUsuarioIdsAsync(trabalhador.ObraId, ct);
        if (tecnicos.Count == 0) return;

        var obraId = trabalhador.ObraId;
        var hoje = AgendaTreinamentosIntegracao.HojeBrasilia();
        var inicioDoDia = AgendaTreinamentosIntegracao.InicioDoDiaUtc(hoje);

        var idsPessoas = await _db.PlanosIntegracao.IgnoreQueryFilters()
            .Where(p => p.ObraId == obraId && p.Ativo && p.Status != StatusPlanoIntegracao.Cancelado && p.CreatedAtUtc >= inicioDoDia)
            .Select(p => p.TrabalhadorId)
            .Distinct()
            .ToListAsync(ct);
        if (idsPessoas.Count == 0) return;

        var nomes = await _db.Trabalhadores.IgnoreQueryFilters()
            .Where(t => idsPessoas.Contains(t.Id))
            .OrderBy(t => t.Nome)
            .Select(t => t.Nome)
            .ToListAsync(ct);
        var obraNome = await _db.Obras.Where(o => o.Id == obraId).Select(o => o.Nome).FirstOrDefaultAsync(ct);

        var reservas = await _db.ReservasEpi
            .Where(r => idsPessoas.Contains(r.TrabalhadorId) && r.ObraId == obraId && r.Status == StatusReservaEpi.Ativa)
            .Select(r => new { r.Id, r.CatalogoEpiId })
            .ToListAsync(ct);
        var separadas = await SituacaoReservasEpi.SeparadasAsync(
            _db, obraId, reservas.Select(r => r.CatalogoEpiId).Distinct().ToList(), ct);
        var emFalta = reservas.Where(r => !separadas.GetValueOrDefault(r.Id)).ToList();
        var idsEpiEmFalta = emFalta.Select(f => f.CatalogoEpiId).Distinct().ToList();
        var nomesEpi = idsEpiEmFalta.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.CatalogoEpis.Where(e => idsEpiEmFalta.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.Nome, ct);
        var faltas = emFalta.GroupBy(r => r.CatalogoEpiId)
            .Select(g => (Nome: nomesEpi.GetValueOrDefault(g.Key, "EPI"), Quantidade: g.Count()))
            .OrderBy(f => f.Nome)
            .Select(f => f.Quantidade > 1 ? $"{f.Nome} ×{f.Quantidade}" : f.Nome)
            .ToList();

        // Treinamentos agendados que NÃO são do pacote (ex.: NR-10, NR-11, NR-33).
        var idsCursos = await _db.ParticipantesSessaoTreinamento
            .Where(p => idsPessoas.Contains(p.TrabalhadorId) && p.Ativo && p.SessaoTreinamento!.Ativo
                && p.SessaoTreinamento.Status == StatusSessaoTreinamento.EmAndamento)
            .Select(p => p.SessaoTreinamento!.CursoTreinamentoId)
            .ToListAsync(ct);
        var cursosAgendados = idsCursos.Count == 0
            ? new List<string>()
            : (await _db.CursosTreinamento.Where(c => idsCursos.Contains(c.Id)).ToListAsync(ct))
                .Where(c => !PacoteIntegracao.EhCursoDoPacote(c) && !c.EhIntegracaoSeguranca)
                .OrderBy(c => c.Nome)
                .Select(c => $"{c.Nome} ({idsCursos.Count(id => id == c.Id)})")
                .ToList();

        var local = string.IsNullOrWhiteSpace(obraNome) ? "na obra" : $"na obra {obraNome}";
        var titulo = idsPessoas.Count == 1
            ? $"1 pessoa entrou {local} hoje"
            : $"{idsPessoas.Count} pessoas entraram {local} hoje";
        var pessoas = nomes.Count <= 5
            ? string.Join(", ", nomes)
            : $"{string.Join(", ", nomes.Take(5))} e mais {nomes.Count - 5}";
        var kit = reservas.Count == 0
            ? "Nenhum EPI a separar."
            : emFalta.Count == 0
                ? $"Kit de EPI: {reservas.Count} item(ns) separado(s)."
                : $"Kit de EPI: {reservas.Count - emFalta.Count} separado(s), {emFalta.Count} em falta ({string.Join(", ", faltas)}).";
        var treinamentos = cursosAgendados.Count == 0
            ? ""
            : $" Treinamentos agendados: {string.Join(", ", cursosAgendados)}.";
        var descricao = $"Fichas de entrada: {pessoas}. {kit}{treinamentos} Abra as fichas de entrada para ver os próximos passos.";
        var severidade = emFalta.Count > 0 ? SeveridadeAlerta.Critico : SeveridadeAlerta.Atencao;

        var chaves = tecnicos.ToDictionary(usuarioId => usuarioId, usuarioId => ChaveAvisoDoDia(obraId, usuarioId, hoje));
        var idsChaves = chaves.Values.ToList();
        var existentes = await _db.Alertas.IgnoreQueryFilters()
            .Where(a => a.Ativo && a.Tipo == TipoAlerta.IntegracaoFuncionarioNovo && a.EntidadeOrigemTipo == OrigemAvisoEntradasObra
                && idsChaves.Contains(a.EntidadeOrigemId) && StatusAlertaEmAberto.Contains(a.Status))
            .ToListAsync(ct);

        var criados = new List<Alerta>();
        foreach (var usuarioId in tecnicos)
        {
            var existente = existentes.FirstOrDefault(a => a.EntidadeOrigemId == chaves[usuarioId] && a.DestinatarioUsuarioId == usuarioId);
            if (existente is not null)
            {
                existente.Titulo = titulo;
                existente.Descricao = descricao;
                existente.Severidade = severidade;
                continue;
            }
            var novo = new Alerta
            {
                Tipo = TipoAlerta.IntegracaoFuncionarioNovo,
                Severidade = severidade,
                Titulo = titulo,
                Descricao = descricao,
                EntidadeOrigemTipo = OrigemAvisoEntradasObra,
                EntidadeOrigemId = chaves[usuarioId],
                ObraId = obraId,
                DestinatarioUsuarioId = usuarioId,
            };
            _db.Alertas.Add(novo);
            criados.Add(novo);
        }
        await _db.SaveChangesAsync(ct);

        // Envio proativo no Teams só na criação do alerta do dia — enfileira e segue em frente
        // (mesmo princípio de AlertaEngineService).
        foreach (var alerta in criados)
            await _filaTeams.EnfileirarAsync(
                new NotificacaoTeamsMensagem(alerta.Id, alerta.DestinatarioUsuarioId!.Value, alerta.Titulo, alerta.Descricao), ct);
    }

    // Guid estável para (obra, técnico, dia de Brasília): é a chave de dedup do aviso agrupado.
    private static Guid ChaveAvisoDoDia(Guid obraId, Guid usuarioId, DateTime dia)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes($"{OrigemAvisoEntradasObra}|{obraId:N}|{usuarioId:N}|{dia:yyyyMMdd}"));
        return new Guid(bytes);
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
