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

        var novasReservas = 0;
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
                novasReservas++;
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
        var opcoes = _opcoes?.Value ?? new OpcoesIntegracao();
        // Troca de função/transferência de quem já tem o pacote (NR-06, NR-18, NR-35) válido não precisa de
        // ficha nova. Só dispensa quando: (a) não há ficha aberta (senão a ficha antiga ficaria apontando para
        // a obra/função anterior: segue o caminho antigo, cancela e abre a nova); (b) não é terceirizado (a
        // liberação dele exige a Integração de Segurança, que o pacote não cobre). Admissão sempre abre ficha.
        // Dispensar só evita abrir a ficha e repetir o pacote: cursos fora do pacote da função/obra nova e o
        // kit de EPI continuam sendo tratados e os técnicos avisados.
        var dispensaFicha = motivo is MotivoPlanoIntegracao.TrocaFuncao or MotivoPlanoIntegracao.TransferenciaObra
            && opcoes.DispensarFichaComPacoteValido
            && planosAbertos.Count == 0
            && trabalhador.Vinculo != TipoVinculo.Terceirizado
            && await PacoteValidoAsync(trabalhadorId, ct);
        var avisar = false;
        if (dispensaFicha)
        {
            // Modo dispensa: agenda todo curso EXIGIDO pela função/obra nova que a pessoa não tem válido, comparando
            // por curso (CursoTreinamentoId, como a liberação) — inclusive NR-12, Integração de Segurança e um NR-35
            // de outro curso. Quem já tem o curso exigido válido é pulado, então o pacote em dia não se repete.
            var turmasNovas = await AgendaTreinamentosIntegracao.AgendarAsync(_db, _geradorNumero, trabalhador, ct, dispensa: true);
            avisar = turmasNovas.Count > 0 || novasReservas > 0;

            // Rastro da dispensa: plano já encerrado com status Dispensado (não aparece no pop-up nem impede uma
            // ficha futura). Dispensa repetida no mesmo dia atualiza o registro em vez de duplicar.
            var inicioDoDia = AgendaTreinamentosIntegracao.InicioDoDiaUtc(AgendaTreinamentosIntegracao.HojeBrasilia());
            var dispensaDeHoje = await _db.PlanosIntegracao.IgnoreQueryFilters()
                .Where(p => p.TrabalhadorId == trabalhadorId && p.Ativo && p.Status == StatusPlanoIntegracao.Dispensado
                    && p.CreatedAtUtc >= inicioDoDia)
                .OrderByDescending(p => p.CreatedAtUtc)
                .FirstOrDefaultAsync(ct);
            if (dispensaDeHoje is null)
            {
                _db.PlanosIntegracao.Add(new PlanoIntegracao
                {
                    TrabalhadorId = trabalhadorId,
                    ObraId = trabalhador.ObraId,
                    FuncaoId = trabalhador.FuncaoId,
                    Motivo = motivo!.Value,
                    Status = StatusPlanoIntegracao.Dispensado,
                    EncerradoEm = agora,
                });
            }
            else
            {
                dispensaDeHoje.ObraId = trabalhador.ObraId;
                dispensaDeHoje.FuncaoId = trabalhador.FuncaoId;
                dispensaDeHoje.Motivo = motivo!.Value;
                dispensaDeHoje.EncerradoEm = agora;
            }
        }
        else if (motivo is not null)
        {
            await AgendaTreinamentosIntegracao.AgendarAsync(_db, _geradorNumero, trabalhador, ct, opcoes.PacotePeloFluxoRealizarIntegracao);
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

        if (planoNovo is not null || avisar)
            await AvisarTecnicosAsync(trabalhador, opcoes.PacotePeloFluxoRealizarIntegracao, ct);

        // Separar ou cancelar kit muda o disponível da obra (alerta de estoque mínimo, Módulo 2).
        if (_verificacaoMinimo is not null) await _verificacaoMinimo.VerificarAsync(trabalhador.ObraId, ct);
    }

    // NR-06, NR-18 e NR-35 com treinamento dentro da validade. A comparação é POR NORMA (não por curso
    // específico): qualquer curso cuja norma normalizada seja NR-06/NR-18/NR-35 conta, e o curso marcado
    // AtendeNr6 vale como NR-06. Data de validade igual à da liberação para o trabalho
    // (ObterLiberacaoParaTrabalhoQuery): DataValidade.Date >= DateTime.UtcNow.Date.
    private async Task<bool> PacoteValidoAsync(Guid trabalhadorId, CancellationToken ct)
    {
        var hoje = DateTime.UtcNow.Date;
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
    // agendadas vão listadas na descrição.
    //
    // Chave de dedup: EntidadeOrigemId é um Guid determinístico de obra + técnico + dia (a obra fica também
    // em ObraId), único por técnico. O Motor de Alertas tolera chave repetida entre alertas em aberto, mas
    // manter a chave única evita que ele escolha um só entre vários.
    private async Task AvisarTecnicosAsync(Trabalhador trabalhador, bool pacoteNoFluxo, CancellationToken ct)
    {
        var tecnicos = await _tecnicos.ObterUsuarioIdsAsync(trabalhador.ObraId, ct);
        if (tecnicos.Count == 0) return;

        var obraId = trabalhador.ObraId;
        var hoje = AgendaTreinamentosIntegracao.HojeBrasilia();
        var inicioDoDia = AgendaTreinamentosIntegracao.InicioDoDiaUtc(hoje);

        // Quem entra no aviso do dia é recalculado do banco só a partir dos PlanosIntegracao criados hoje na obra
        // (Aberto, Concluído ou Dispensado; não Cancelado). A dispensa deixa registro (status Dispensado), então
        // quem foi dispensado continua na conta quando outra ficha atualiza o alerta, sem contar inscrições nem
        // reservas avulsas do dia.
        var planosDeHoje = await _db.PlanosIntegracao.IgnoreQueryFilters()
            .Where(p => p.ObraId == obraId && p.Ativo && p.Status != StatusPlanoIntegracao.Cancelado && p.CreatedAtUtc >= inicioDoDia)
            .Select(p => new { p.TrabalhadorId, p.Status })
            .ToListAsync(ct);
        var idsComFicha = planosDeHoje.Where(p => p.Status != StatusPlanoIntegracao.Dispensado)
            .Select(p => p.TrabalhadorId).Distinct().ToList();
        var idsSemFicha = planosDeHoje.Select(p => p.TrabalhadorId).Distinct().Except(idsComFicha).ToList();
        var idsPessoas = idsComFicha.Concat(idsSemFicha).ToList();
        if (idsPessoas.Count == 0) return;

        var pessoasDoDia = await _db.Trabalhadores.IgnoreQueryFilters()
            .Where(t => idsPessoas.Contains(t.Id))
            .OrderBy(t => t.Nome)
            .Select(t => new { t.Id, t.Nome, t.Vinculo })
            .ToListAsync(ct);
        var nomesComFicha = pessoasDoDia.Where(p => idsComFicha.Contains(p.Id)).Select(p => p.Nome).ToList();
        var nomesSemFicha = pessoasDoDia.Where(p => !idsComFicha.Contains(p.Id)).Select(p => p.Nome).ToList();
        var temTerceirizado = pessoasDoDia.Any(p => p.Vinculo == TipoVinculo.Terceirizado);
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

        // Treinamentos agendados hoje para essas pessoas (inclui NR-12/Integração agendados na dispensa).
        var idsCursos = await _db.ParticipantesSessaoTreinamento
            .Where(p => idsPessoas.Contains(p.TrabalhadorId) && p.Ativo && p.CreatedAtUtc >= inicioDoDia && p.SessaoTreinamento!.Ativo
                && p.SessaoTreinamento.Status == StatusSessaoTreinamento.EmAndamento)
            .Select(p => p.SessaoTreinamento!.CursoTreinamentoId)
            .ToListAsync(ct);
        var cursosAgendados = idsCursos.Count == 0
            ? new List<string>()
            : (await _db.CursosTreinamento.Where(c => idsCursos.Contains(c.Id)).ToListAsync(ct))
                .OrderBy(c => c.Nome)
                .Select(c => $"{c.Nome} ({idsCursos.Count(id => id == c.Id)})")
                .ToList();

        var local = string.IsNullOrWhiteSpace(obraNome) ? "na obra" : $"na obra {obraNome}";
        var titulo = idsPessoas.Count == 1
            ? $"1 pessoa com entrada ou mudança de função {local} hoje"
            : $"{idsPessoas.Count} pessoas com entrada ou mudança de função {local} hoje";
        static string Lista(List<string> nomes) => nomes.Count <= 5
            ? string.Join(", ", nomes)
            : $"{string.Join(", ", nomes.Take(5))} e mais {nomes.Count - 5}";
        var pessoas = (nomesComFicha.Count > 0 ? $"Fichas de entrada: {Lista(nomesComFicha)}. " : "")
            + (nomesSemFicha.Count > 0 ? $"Sem ficha nova (pacote em dia): {Lista(nomesSemFicha)} — kit e cursos da nova função abaixo. " : "");
        // Terceirizado não tem ReservaEpi: o kit dele segue pelo contrato da empresa (não é "nenhum EPI").
        var kitContrato = temTerceirizado ? "O kit de EPI de terceirizados segue pelo contrato da empresa." : "";
        var kit = reservas.Count == 0
            ? (temTerceirizado ? kitContrato : "Nenhum EPI a separar.")
            : (emFalta.Count == 0
                ? $"Kit de EPI: {reservas.Count} item(ns) separado(s)."
                : $"Kit de EPI: {reservas.Count - emFalta.Count} separado(s), {emFalta.Count} em falta ({string.Join(", ", faltas)}).")
              + (temTerceirizado ? $" {kitContrato}" : "");
        var treinamentos = cursosAgendados.Count == 0
            ? ""
            : $" Treinamentos agendados: {string.Join(", ", cursosAgendados)}.";
        // Nome atual do pop-up; com o fluxo em lote ligado o caminho passa a ser "Realizar integração".
        var destino = pacoteNoFluxo ? "Realizar integração" : "Funcionário novo";
        var chamada = nomesComFicha.Count > 0 ? $" Abra \"{destino}\" para ver os próximos passos das fichas." : "";
        var descricao = $"{pessoas}{kit}{treinamentos}{chamada}";
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
            // Se uma corrida deixou mais de um aberto com a mesma chave, atualiza o mais recente.
            var existente = existentes
                .Where(a => a.EntidadeOrigemId == chaves[usuarioId] && a.DestinatarioUsuarioId == usuarioId)
                .OrderByDescending(a => a.CreatedAtUtc)
                .FirstOrDefault();
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
