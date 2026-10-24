using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Asos.Commands;

// Upsert de Aso a partir da leitura direta do banco do G-RH (Integração G-RH — leitura de banco em
// vez de endpoint HTTP, decisão de 2026-09-16 por indisponibilidade momentânea do time do G-RH; ver
// AsoGrhDbClient). Usado tanto pelo polling contínuo (GrhDbPollingService) quanto pela carga inicial
// (ImportarAsoGrhCommand, em lote).
//
// Regras de negócio importantes:
// - DataValidade é obrigatória no SST (usada por AsoValidoRule/AsoAlertaProvider) — se o G-RH ainda
//   não tiver preenchido, o registro é rejeitado (não cria "ASO incompleto"); a próxima rodada de
//   polling pega automaticamente assim que a validade existir lá.
// - ResultadoStatus/MedicoNome/Restricoes só são sobrescritos quando o G-RH manda valor preenchido —
//   nunca apagam uma avaliação clínica que o médico do trabalho já lançou manualmente no SST.
// - Tipo vem de `classificacao_exame` do G-RH (ADMISSIONAL/PERIODICO/...). Registro antigo sem
//   classificação fica como Periodico (novo) ou mantém o tipo que já tinha no SST.
// - O G-RH cadastra exames complementares (Audiometria, Espirometria...) com o tipo de documento ASO,
//   sem aptidão. Eles não são ASO: não entram no SST, e um que já tinha entrado é inativado (Ativo =
//   false, reversível) — senão vira um ASO "Pendente" na mesma data do ASO real e esconde o resultado.
public record SincronizarAsoGrhCommand(
    string GrhAsoId,
    string Cpf,
    DateTime DataExame,
    DateTime? DataValidade,
    string? Aptidao,
    string? RestricaoClinica,
    string? MedicoNome,
    string? Numero = null,
    string? ClassificacaoExame = null) : IRequest<Guid>
{
    public static SincronizarAsoGrhCommand DoAsoGrh(AsoGrhDto a) =>
        new(a.GrhAsoId, CpfMascarador.ApenasDigitos(a.Cpf), a.DataExame, a.DataValidade, a.Aptidao, a.RestricaoClinica, a.MedicoNome,
            a.Numero, a.ClassificacaoExame);
}

public class SincronizarAsoGrhCommandHandler : IRequestHandler<SincronizarAsoGrhCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly ICpfHashService _cpfHash;

    public SincronizarAsoGrhCommandHandler(IAppDbContext db, ICpfHashService cpfHash)
    {
        _db = db;
        _cpfHash = cpfHash;
    }

    public async Task<Guid> Handle(SincronizarAsoGrhCommand request, CancellationToken ct)
    {
        if (EhExameComplementar(request.Numero))
            return await InativarExameComplementarAsync(request.GrhAsoId, ct);

        if (request.DataValidade is null)
            throw new InvalidOperationException(
                "ASO ainda sem validade preenchida no G-RH — sincronização adiada até o campo existir lá.");

        var hash = _cpfHash.CalcularHash(request.Cpf);
        var trabalhador = await _db.Trabalhadores.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.CpfHash == hash, ct);
        if (trabalhador is null)
            throw new InvalidOperationException(
                "Trabalhador com o CPF informado não encontrado no SST — ASO do G-RH não sincronizado.");

        // IgnoreQueryFilters: mesmo motivo de SincronizarAlojamentoGrhCommand — enxerga registros
        // soft-deletados localmente pra reativar em vez de duplicar.
        var aso = await _db.Asos.IgnoreQueryFilters()
            .Include(a => a.Restricoes)
            .FirstOrDefaultAsync(a => a.GrhAsoId == request.GrhAsoId, ct);

        if (aso is null)
        {
            aso = new Aso
            {
                TrabalhadorId = trabalhador.Id,
                GrhAsoId = request.GrhAsoId,
                Tipo = MapearClassificacao(request.ClassificacaoExame) ?? TipoExameAso.Periodico,
                ResultadoStatus = ResultadoAso.Pendente,
            };
            _db.Asos.Add(aso);
        }
        else
        {
            aso.Ativo = true; // reativa se alguém excluiu manualmente pela tela
        }

        var tipo = MapearClassificacao(request.ClassificacaoExame);
        if (tipo is not null)
            aso.Tipo = tipo.Value;

        aso.DataExame = request.DataExame;
        aso.DataValidade = request.DataValidade.Value;
        aso.DataUltimaSincronizacao = DateTime.UtcNow;

        var resultado = MapearAptidao(request.Aptidao);
        if (resultado is not null)
            aso.ResultadoStatus = resultado.Value;

        if (!string.IsNullOrWhiteSpace(request.MedicoNome))
            aso.MedicoNome = request.MedicoNome;

        if (!string.IsNullOrWhiteSpace(request.RestricaoClinica))
        {
            var descricao = request.RestricaoClinica.Trim();
            if (descricao.Length > 300) descricao = descricao[..300];
            if (!aso.Restricoes.Any(r => r.Descricao == descricao))
                aso.Restricoes.Add(new AsoRestricao { Descricao = descricao });
        }

        await _db.SaveChangesAsync(ct);
        return aso.Id;
    }

    // "ASO", "ASO Periódico" etc. são ASO; vazio também (registro antigo sem nome). Qualquer outro
    // nome ("Audiometria Tonal", "Espirometria") é exame complementar.
    public static bool EhExameComplementar(string? numero) =>
        !string.IsNullOrWhiteSpace(numero)
        && !numero.Trim().StartsWith("ASO", StringComparison.OrdinalIgnoreCase);

    private async Task<Guid> InativarExameComplementarAsync(string grhAsoId, CancellationToken ct)
    {
        var existente = await _db.Asos.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.GrhAsoId == grhAsoId, ct);
        if (existente is null) return Guid.Empty;

        if (existente.Ativo)
        {
            existente.Ativo = false;
            existente.DataUltimaSincronizacao = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return existente.Id;
    }

    private static TipoExameAso? MapearClassificacao(string? classificacao) =>
        RemoverAcentos(classificacao?.Trim().ToUpperInvariant()) switch
        {
            "ADMISSIONAL" => TipoExameAso.Admissional,
            "PERIODICO" => TipoExameAso.Periodico,
            "RETORNO AO TRABALHO" or "RETORNO_AO_TRABALHO" or "RETORNO" => TipoExameAso.RetornoAoTrabalho,
            "MUDANCA DE FUNCAO" or "MUDANCA_DE_FUNCAO" or "MUDANCA DE RISCO" => TipoExameAso.MudancaDeFuncao,
            "DEMISSIONAL" => TipoExameAso.Demissional,
            _ => null,
        };

    private static string? RemoverAcentos(string? texto) => texto?
        .Replace('Á', 'A').Replace('Ã', 'A').Replace('Â', 'A').Replace('É', 'E').Replace('Ê', 'E')
        .Replace('Í', 'I').Replace('Ó', 'O').Replace('Õ', 'O').Replace('Ô', 'O').Replace('Ú', 'U').Replace('Ç', 'C');

    private static ResultadoAso? MapearAptidao(string? aptidao) => aptidao?.Trim().ToUpperInvariant() switch
    {
        "APTO" => ResultadoAso.Apto,
        "APTO COM RESTRICAO" or "APTO COM RESTRIÇÃO" => ResultadoAso.AptoComRestricao,
        "INAPTO" => ResultadoAso.Inapto,
        _ => null,
    };
}
