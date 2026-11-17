using AAHBRANT.SST.Application.Acidentes;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Acidentes.Commands;

// Avança um passo no fluxo não-literal (ver Domain/Enums/Enums.cs, StatusAcidente):
// Registrado → Em investigação → Concluído. "Aguardando laudo" é uma pausa da investigação
// (AguardarLaudoCommand / RetomarInvestigacaoCommand): daqui não se conclui, é preciso retomar antes.
// Bloqueio de negócio (decisão própria, mesmo padrão de "bloqueio preventivo" usado em
// AvancarStatusNaoConformidadeCommand/EncerrarInspecaoCommand): o último passo (EmInvestigacao →
// Concluido) só é permitido se todo AcaoPlano vinculado (via OrigemTipo=nameof(Acidente)/OrigemId)
// já estiver com Status == Concluido. Desde 23/10/2026 também exige a reunião de análise registrada
// como realizada, em qualquer tipo de ocorrência. Sem nenhuma ação do plano vinculada, a conclusão
// exige a justificativa (ConclusaoInvestigacao); havendo ações, ela é opcional.
public record AvancarStatusAcidenteCommand(Guid Id, string? ConclusaoInvestigacao = null) : IRequest;

public class AvancarStatusAcidenteCommandValidator : AbstractValidator<AvancarStatusAcidenteCommand>
{
    public AvancarStatusAcidenteCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ConclusaoInvestigacao).MaximumLength(4000);
    }
}

public class AvancarStatusAcidenteCommandHandler : IRequestHandler<AvancarStatusAcidenteCommand>
{
    // Exigência de 3 fotos para QUALQUER tipo de ocorrência (decisão de 17/11/2026). Para não travar
    // retroativamente as ocorrências já registradas, vale só para as criadas a partir desta data (UTC);
    // as anteriores mantêm a regra antiga (só TipoOcorrencia.Acidente exige as 3 fotos). O corte é a
    // meia-noite de Brasília (2026-11-18 00:00 America/Sao_Paulo, UTC-3 = 03:00 UTC), comparada com CreatedAtUtc.
    public static readonly DateTime ExigeTresFotosParaTodosOsTiposApartirDe = new(2026, 11, 18, 3, 0, 0, DateTimeKind.Utc);

    // Regra única, usada pelo comando e pelas queries (AcidenteDto.ExigeTresFotos).
    public static bool ExigeTresFotos(TipoOcorrencia tipo, DateTime createdAtUtc) =>
        tipo == TipoOcorrencia.Acidente
        || createdAtUtc >= ExigeTresFotosParaTodosOsTiposApartirDe;

    public static bool ExigeTresFotos(Acidente acidente) =>
        ExigeTresFotos(acidente.Tipo, acidente.CreatedAtUtc);

    private readonly IAppDbContext _db;
    private readonly IPublicadorAcidenteGrh _publicadorGrh;

    public AvancarStatusAcidenteCommandHandler(IAppDbContext db, IPublicadorAcidenteGrh publicadorGrh)
    {
        _db = db;
        _publicadorGrh = publicadorGrh;
    }

    public async Task Handle(AvancarStatusAcidenteCommand request, CancellationToken ct)
    {
        var acidente = await _db.Acidentes.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Acidente {request.Id} não encontrado.");

        if (acidente.Status == StatusAcidente.Concluido)
            throw new InvalidOperationException("Este acidente já está com a investigação concluída.");

        if (acidente.Status == StatusAcidente.AguardandoLaudo)
            throw new InvalidOperationException("A investigação está aguardando o laudo. Retome a investigação antes de concluir.");

        if (acidente.Status == StatusAcidente.EmInvestigacao)
        {
            if (ExigeTresFotos(acidente))
            {
                var fotos = await _db.AcidentesFotos.Where(f => f.AcidenteId == acidente.Id).ToListAsync(ct);
                if (fotos.Select(f => f.Ordem).Distinct().Count() != 3)
                    throw new InvalidOperationException("São obrigatórias três fotos da ocorrência para finalizar a investigação.");
                foreach (var foto in fotos)
                    AAHBRANT.SST.Application.Common.DadosCapturaFoto.ExigirCompleta(foto.FotoMetadadosJson, $"Foto {foto.Ordem}");
            }
            var reuniaoRealizada = await _db.ReunioesAnaliseOcorrencia.AnyAsync(
                r => r.AcidenteId == acidente.Id && r.RealizadaEm != null, ct);
            if (!reuniaoRealizada)
                throw new InvalidOperationException(
                    "Registre a reunião de análise como realizada antes de finalizar a investigação.");

            await AAHBRANT.SST.Application.AcoesPlano.ResolucaoVinculadaAcaoPlano.ConcluirAtendidasAsync(_db, new[] { acidente.Id }, ct);
            var existeAcaoPendente = await _db.AcoesPlano.AnyAsync(
                a => a.OrigemTipo == nameof(Domain.Entidades.Acidente) &&
                     a.OrigemId == acidente.Id &&
                     a.Status != StatusControleRisco.Concluido,
                ct);

            if (existeAcaoPendente)
                throw new InvalidOperationException(
                    "Não é possível concluir: existem ações do plano vinculadas ainda não concluídas.");

            var conclusao = string.IsNullOrWhiteSpace(request.ConclusaoInvestigacao) ? null : request.ConclusaoInvestigacao.Trim();
            // A reunião de análise (obrigatória) cria sempre uma AcaoPlano da própria ocorrência; ela não conta
            // como "ação do plano": senão a justificativa nunca seria exigida.
            var acaoDaReuniao = await _db.ReunioesAnaliseOcorrencia
                .Where(r => r.AcidenteId == acidente.Id && r.AcaoPlanoId != null)
                .Select(r => r.AcaoPlanoId!.Value).ToListAsync(ct);
            var temAcaoVinculada = await _db.AcoesPlano.AnyAsync(
                a => a.OrigemTipo == nameof(Domain.Entidades.Acidente) && a.OrigemId == acidente.Id
                     && !acaoDaReuniao.Contains(a.Id), ct);
            if (!temAcaoVinculada && conclusao is null && string.IsNullOrWhiteSpace(acidente.ConclusaoInvestigacao))
                throw new InvalidOperationException(
                    "Sem ações no plano, informe a justificativa da conclusão da investigação.");
            if (conclusao is not null)
                acidente.ConclusaoInvestigacao = conclusao;

            acidente.DataConclusaoInvestigacao = DateTime.UtcNow;
        }

        acidente.Status = acidente.Status switch
        {
            StatusAcidente.Registrado => StatusAcidente.EmInvestigacao,
            StatusAcidente.EmInvestigacao => StatusAcidente.Concluido,
            _ => acidente.Status,
        };

        await _db.SaveChangesAsync(ct);
        await AcidenteGrhEventoFactory.PublicarAsync(_db, _publicadorGrh, acidente, ct);
    }
}
