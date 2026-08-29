using AAHBRANT.SST.Application.AcoesPlano;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.NaoConformidades.Commands;

// Procedimento de Inspeção Técnica de Campo (§6.5) — botão RESPONDER: o responsável informa a ação
// que será realizada, confirma/define quem vai executar e a data prevista de conclusão. Aqui isso
// cria a AcaoPlano vinculada (OrigemTipo=nameof(NaoConformidade)) e move a NC direto de
// Enviada/Devolvida para EmAndamento — o estado "Em análise" do §9 fica reservado no enum
// (StatusNaoConformidade.EmAnalise) para uma futura distinção entre "responsável abriu" e
// "responsável já respondeu", não implementada nesta fatia (decisão de escopo, não literal do
// documento).
public record ResponderNaoConformidadeCommand(
    Guid NaoConformidadeId,
    string DescricaoAcao,
    Guid? ResponsavelExecucaoId,
    PrioridadeAcao Prioridade,
    DateTime? Prazo,
    string? JustificativaPrazo) : IRequest<Guid>;

public class ResponderNaoConformidadeCommandValidator : AbstractValidator<ResponderNaoConformidadeCommand>
{
    public ResponderNaoConformidadeCommandValidator()
    {
        RuleFor(x => x.NaoConformidadeId).NotEmpty();
        RuleFor(x => x.DescricaoAcao).NotEmpty().MaximumLength(500);
        RuleFor(x => x.JustificativaPrazo).MaximumLength(500);
    }
}

public class ResponderNaoConformidadeCommandHandler : IRequestHandler<ResponderNaoConformidadeCommand, Guid>
{
    private readonly IAppDbContext _db;

    public ResponderNaoConformidadeCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(ResponderNaoConformidadeCommand request, CancellationToken ct)
    {
        var nc = await _db.NaoConformidades.FirstOrDefaultAsync(n => n.Id == request.NaoConformidadeId, ct)
            ?? throw new KeyNotFoundException($"Não conformidade {request.NaoConformidadeId} não encontrada.");

        if (nc.Status is not (StatusNaoConformidade.Enviada or StatusNaoConformidade.Devolvida))
            throw new InvalidOperationException(
                "Só é possível responder uma ocorrência que esteja Enviada ou Devolvida.");

        var responsavelId = request.ResponsavelExecucaoId ?? nc.ResponsavelUsuarioId;
        if (responsavelId.HasValue && !await _db.Usuarios.AnyAsync(u => u.Id == responsavelId, ct))
            throw new KeyNotFoundException($"Usuário {responsavelId} não encontrado.");

        // §7: prazo sugerido a partir da prioridade quando não informado explicitamente — mesma
        // regra usada por CriarAcaoPlanoCommand (ver SlaPrioridadeCalculator).
        var prazo = request.Prazo ?? SlaPrioridadeCalculator.CalcularPrazoSugerido(request.Prioridade, DateTime.UtcNow);

        var acao = new AcaoPlano
        {
            OrigemTipo = nameof(NaoConformidade),
            OrigemId = nc.Id,
            Tipo = TipoAcaoPlano.Corretiva,
            Descricao = request.JustificativaPrazo is { Length: > 0 }
                ? $"{request.DescricaoAcao} (prazo justificado: {request.JustificativaPrazo})"
                : request.DescricaoAcao,
            ResponsavelUsuarioId = responsavelId,
            Prioridade = request.Prioridade,
            Prazo = prazo,
        };
        _db.AcoesPlano.Add(acao);

        nc.ResponsavelUsuarioId = responsavelId;
        nc.Prazo = prazo;
        nc.Status = StatusNaoConformidade.EmAndamento;

        await _db.SaveChangesAsync(ct);
        return acao.Id;
    }
}
