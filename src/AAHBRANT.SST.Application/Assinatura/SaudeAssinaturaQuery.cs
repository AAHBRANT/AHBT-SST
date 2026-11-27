using System.Text.Json.Serialization;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.PendenciasAutomaticas;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura;

// Relatório de saúde da assinatura (facial e digital) para o agente engenheiro do Claude Console e para a
// vigia do Worker (pedido do usuário, 27/11, depois de o Azure recusar o endereço do recurso às 22h e a obra
// só descobrir no DDS das 6h58). Só leitura. LGPD: só contagens e ids técnicos, nunca nome, CPF, foto ou
// biometria — o relatório sai do sistema para o agente.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SeveridadeAlertaAssinatura
{
    Atencao = 1,
    Critico = 2,
}

public record AlertaAssinaturaDto(SeveridadeAlertaAssinatura Severidade, string Codigo, Guid? ObraId, string? ObraNome, string Mensagem, string AcaoSugerida);

public record FalhasFaciaisDto(int Total, int ConfiancaBaixa, int RostoNaoReconhecido, int RostoAmbiguo, int CadastroSemVinculo);

public record LeitorDigitalDto(Guid Id, string Nome, DateTime? UltimaSincronizacaoEmUtc, int? HorasSemContato);

public record SaudeObraAssinaturaDto(
    Guid ObraId,
    string ObraNome,
    string? GrupoAzure,
    bool? GrupoExisteNoAzure,
    string? TreinoStatus,
    DateTime? TreinoUltimaAcaoEmUtc,
    int? CadastrosFaciaisNoBanco,
    int? PessoasNoAzure,
    int? VinculosQuebrados,
    int? PessoasSemVinculo,
    int? DuplicadosNoBanco,
    string? ErroDiagnostico,
    FalhasFaciaisDto Falhas24h,
    int TrabalhadoresAtivos,
    int SemCadastroFacial,
    int DigitaisIncompletas,
    IReadOnlyList<LeitorDigitalDto> LeitoresDigital);

// Situacao: "ok" (nenhum alerta), "atencao" ou "critico" (o pior alerta).
public record SaudeAssinaturaDto(
    DateTime GeradoEmUtc,
    string Situacao,
    VerificacaoServicoFacialDto ServicoFacial,
    IReadOnlyList<AlertaAssinaturaDto> Alertas,
    IReadOnlyList<SaudeObraAssinaturaDto> Obras);

public record ObterSaudeAssinaturaQuery : IRequest<SaudeAssinaturaDto>;

public class ObterSaudeAssinaturaQueryHandler : IRequestHandler<ObterSaudeAssinaturaQuery, SaudeAssinaturaDto>
{
    // Acima disso em 24 h, a fila facial da obra está recusando gente demais: vale olhar antes do próximo DDS.
    public const int LimiteFalhas24h = 10;

    // O agente da digital sincroniza ao abrir a fila; fim de semana sem DDS é normal. 72 h sem contato em
    // dia útil já indica PC desligado, agente parado ou leitor desconectado.
    public const int LimiteHorasLeitorSemContato = 72;

    private readonly IAppDbContext _db;
    private readonly IAutenticacaoFacialService _facial;

    public ObterSaudeAssinaturaQueryHandler(IAppDbContext db, IAutenticacaoFacialService facial)
    {
        _db = db;
        _facial = facial;
    }

    public async Task<SaudeAssinaturaDto> Handle(ObterSaudeAssinaturaQuery request, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var servico = await _facial.VerificarServicoAsync(ct);

        var obras = await _db.Obras
            .Where(o => o.AzureFacePersonGroupId != null || _db.DispositivosAgenteBiometrico.Any(d => d.ObraId == o.Id))
            .OrderBy(o => o.Nome)
            .Select(o => new { o.Id, o.Nome, o.AzureFacePersonGroupId })
            .ToListAsync(ct);
        var idsObras = obras.Select(o => o.Id).ToList();

        var desde = agora.AddHours(-24);
        var falhas = await _db.FalhasReconhecimentoFacial
            .Where(f => idsObras.Contains(f.ObraId) && f.OcorridaEm >= desde)
            .GroupBy(f => new { f.ObraId, f.Motivo })
            .Select(g => new { g.Key.ObraId, g.Key.Motivo, Total = g.Count() })
            .ToListAsync(ct);

        var leitores = await _db.DispositivosAgenteBiometrico
            .Where(d => idsObras.Contains(d.ObraId))
            .OrderBy(d => d.Nome)
            .Select(d => new { d.Id, d.ObraId, d.Nome, d.UltimaSincronizacaoEm })
            .ToListAsync(ct);

        var biometria = await ListarPendenciasAutomaticasQueryHandler.SituacaoBiometricaAsync(_db, ct);

        var alertas = new List<AlertaAssinaturaDto>();
        if (!servico.Ok)
            alertas.Add(new AlertaAssinaturaDto(SeveridadeAlertaAssinatura.Critico, "SERVICO_FACIAL_FORA", null, null,
                $"O Azure Face não respondeu como deveria ({servico.Endpoint}: {servico.StatusHttp?.ToString() ?? "sem resposta"} {servico.CodigoErro} {servico.Mensagem}).".Trim(),
                "Toda assinatura facial vai falhar. Orientar o uso da digital e investigar o recurso do Azure (endereço, chave, cota)."));
        else if (!servico.Endpoint.EndsWith(".cognitiveservices.azure.com", StringComparison.OrdinalIgnoreCase))
            alertas.Add(new AlertaAssinaturaDto(SeveridadeAlertaAssinatura.Atencao, "ENDPOINT_FACIAL_REGIONAL", null, null,
                $"O reconhecimento facial usa o endereço regional {servico.Endpoint}, não um domínio próprio do recurso.",
                "Registrar um domínio personalizado no recurso do Azure e trocar o endereço na API e no Worker."));

        var resultadoObras = new List<SaudeObraAssinaturaDto>();
        foreach (var obra in obras)
        {
            DiagnosticoVinculosFacialDto? diagnostico = null;
            TreinoGrupoFacialDto? treino = null;
            string? erroDiagnostico = null;
            // Com o serviço fora, a consulta por obra só repetiria o mesmo erro.
            if (obra.AzureFacePersonGroupId is not null && servico.Ok)
            {
                try
                {
                    diagnostico = await _facial.DiagnosticarVinculosAsync(obra.Id, ct);
                    treino = await _facial.ObterTreinoGrupoAsync(obra.Id, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    erroDiagnostico = ex.Message;
                }
            }

            int Falhas(MotivoFalhaFacial motivo) => falhas.Where(f => f.ObraId == obra.Id && f.Motivo == motivo).Sum(f => f.Total);
            var falhas24h = new FalhasFaciaisDto(
                falhas.Where(f => f.ObraId == obra.Id).Sum(f => f.Total),
                Falhas(MotivoFalhaFacial.ConfiancaBaixa),
                Falhas(MotivoFalhaFacial.RostoNaoReconhecido),
                Falhas(MotivoFalhaFacial.RostoAmbiguo),
                Falhas(MotivoFalhaFacial.CadastroSemVinculo));

            var leitoresObra = leitores.Where(l => l.ObraId == obra.Id)
                .Select(l => new LeitorDigitalDto(l.Id, l.Nome, l.UltimaSincronizacaoEm,
                    l.UltimaSincronizacaoEm is { } ultima ? (int)Math.Floor((agora - ultima).TotalHours) : null))
                .ToList();

            var pessoasDaObra = biometria.Where(b => b.ObraId == obra.Id).ToList();

            var saude = new SaudeObraAssinaturaDto(
                obra.Id, obra.Nome, obra.AzureFacePersonGroupId,
                diagnostico?.GrupoExisteNoAzure,
                treino?.Status, treino?.UltimaAcaoEm,
                diagnostico?.CadastrosNoBanco, diagnostico?.PessoasNoAzure,
                diagnostico?.VinculosQuebrados.Count, diagnostico?.PessoasSemVinculo.Count, diagnostico?.DuplicadosNoBanco.Count,
                erroDiagnostico,
                falhas24h,
                pessoasDaObra.Count,
                pessoasDaObra.Count(p => p.Falta(TipoPendenciaAutomatica.RecadastroFacial) is not null),
                pessoasDaObra.Count(p => p.Falta(TipoPendenciaAutomatica.DigitaisIncompletas) is not null),
                leitoresObra);
            resultadoObras.Add(saude);
            alertas.AddRange(AlertasDaObra(saude));
        }

        var situacao = alertas.Count == 0 ? "ok"
            : alertas.Any(a => a.Severidade == SeveridadeAlertaAssinatura.Critico) ? "critico" : "atencao";
        return new SaudeAssinaturaDto(agora, situacao, servico,
            alertas.OrderByDescending(a => a.Severidade).ThenBy(a => a.ObraNome).ToList(), resultadoObras);
    }

    internal static IEnumerable<AlertaAssinaturaDto> AlertasDaObra(SaudeObraAssinaturaDto o)
    {
        AlertaAssinaturaDto Alerta(SeveridadeAlertaAssinatura severidade, string codigo, string mensagem, string acao)
            => new(severidade, codigo, o.ObraId, o.ObraNome, mensagem, acao);

        if (o.ErroDiagnostico is not null)
            yield return Alerta(SeveridadeAlertaAssinatura.Atencao, "DIAGNOSTICO_FACIAL_FALHOU",
                $"Não foi possível conferir o grupo de rostos: {o.ErroDiagnostico}",
                "Rodar o diagnóstico facial da obra de novo e ver o log da API.");

        // Obra que ainda não cadastrou ninguém no facial não tem o que quebrar (hml 27/11: "Produção Next",
        // 0 de 33 cadastrados, virava alerta crítico todo dia). O grupo nasce no primeiro cadastro.
        if (o.GrupoExisteNoAzure == false && o.CadastrosFaciaisNoBanco > 0)
            yield return Alerta(SeveridadeAlertaAssinatura.Critico, "GRUPO_FACIAL_INEXISTENTE",
                $"O grupo de rostos da obra não existe no Azure: as {o.CadastrosFaciaisNoBanco} pessoa(s) cadastradas no facial não são reconhecidas.",
                "Recriar o grupo da obra a partir das fotos de cadastro (ação do Administrador).");

        if (string.Equals(o.TreinoStatus, "failed", StringComparison.OrdinalIgnoreCase))
            yield return Alerta(SeveridadeAlertaAssinatura.Critico, "TREINO_FACIAL_FALHOU",
                "O último treino do grupo de rostos falhou no Azure.",
                "Retreinar o grupo da obra (ação segura).");
        else if (o.GrupoExisteNoAzure == true && o.TreinoStatus is null)
            yield return Alerta(SeveridadeAlertaAssinatura.Atencao, "TREINO_FACIAL_DESCONHECIDO",
                "O Azure não informou a situação do treino do grupo de rostos.",
                "Retreinar o grupo da obra (ação segura).");

        if (o.VinculosQuebrados > 0)
            yield return Alerta(SeveridadeAlertaAssinatura.Atencao, "VINCULOS_FACIAIS_QUEBRADOS",
                $"{o.VinculosQuebrados} cadastro(s) facial(is) apontam para uma pessoa que não existe no grupo: essas pessoas nunca são reconhecidas.",
                "Refazer o cadastro facial delas (Diagnóstico facial da obra).");

        if (o.PessoasSemVinculo > 0)
            yield return Alerta(SeveridadeAlertaAssinatura.Atencao, "PESSOAS_FACIAIS_SOLTAS",
                $"{o.PessoasSemVinculo} pessoa(s) no grupo do Azure sem funcionário ligado: causam recusas \"sem vínculo\" e \"ambíguo\".",
                "Excluir essas pessoas do grupo e retreinar. Ação irreversível: só com aprovação do responsável.");

        if (o.DuplicadosNoBanco > 0)
            yield return Alerta(SeveridadeAlertaAssinatura.Atencao, "CADASTROS_FACIAIS_DUPLICADOS",
                $"{o.DuplicadosNoBanco} funcionário(s) apontam para a mesma pessoa do Azure.",
                "Conferir no Diagnóstico facial da obra e refazer o cadastro de quem está errado.");

        if (o.Falhas24h.Total >= LimiteFalhas24h)
            yield return Alerta(SeveridadeAlertaAssinatura.Atencao, "MUITAS_RECUSAS_FACIAIS",
                $"{o.Falhas24h.Total} recusas no reconhecimento facial nas últimas 24 h " +
                $"(confiança baixa {o.Falhas24h.ConfiancaBaixa}, não reconhecido {o.Falhas24h.RostoNaoReconhecido}, " +
                $"ambíguo {o.Falhas24h.RostoAmbiguo}, sem vínculo {o.Falhas24h.CadastroSemVinculo}).",
                "Ver a lista de cadastros fracos e refazer o cadastro de quem mais falha.");

        foreach (var leitor in o.LeitoresDigital.Where(l => l.HorasSemContato is null or > LimiteHorasLeitorSemContato))
            yield return Alerta(SeveridadeAlertaAssinatura.Atencao, "LEITOR_DIGITAL_SEM_CONTATO",
                leitor.HorasSemContato is null
                    ? $"O leitor de digital \"{leitor.Nome}\" nunca sincronizou."
                    : $"O leitor de digital \"{leitor.Nome}\" está sem contato há {leitor.HorasSemContato} h.",
                "Conferir se o PC da obra está ligado e se o agente de biometria está rodando.");
    }
}
