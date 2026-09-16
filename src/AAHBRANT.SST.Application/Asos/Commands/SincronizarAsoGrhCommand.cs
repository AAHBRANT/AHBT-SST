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
// - Tipo não existe como campo distinto no G-RH (a tabela `documentos` não separa admissional/
//   periódico/demissional) — mapeado como Periodico por padrão até o G-RH passar a distinguir.
public record SincronizarAsoGrhCommand(
    string GrhAsoId,
    string Cpf,
    DateTime DataExame,
    DateTime? DataValidade,
    string? Aptidao,
    string? RestricaoClinica,
    string? MedicoNome) : IRequest<Guid>
{
    public static SincronizarAsoGrhCommand DoAsoGrh(AsoGrhDto a) =>
        new(a.GrhAsoId, a.Cpf, a.DataExame, a.DataValidade, a.Aptidao, a.RestricaoClinica, a.MedicoNome);
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
                Tipo = TipoExameAso.Periodico,
                ResultadoStatus = ResultadoAso.Pendente,
            };
            _db.Asos.Add(aso);
        }
        else
        {
            aso.Ativo = true; // reativa se alguém excluiu manualmente pela tela
        }

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

    private static ResultadoAso? MapearAptidao(string? aptidao) => aptidao?.Trim().ToUpperInvariant() switch
    {
        "APTO" => ResultadoAso.Apto,
        "APTO COM RESTRICAO" or "APTO COM RESTRIÇÃO" => ResultadoAso.AptoComRestricao,
        "INAPTO" => ResultadoAso.Inapto,
        _ => null,
    };
}
