using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura.Commands;

// Ponto de entrada genérico do motor (§3/§5 do doc, etapa 6) — qualquer módulo que precise de
// assinatura (Dds hoje, Treinamento/EPI/APR/PT/Inspeções depois) chama isso passando seu próprio
// EntidadeTipo/EntidadeId. Idempotente: se já existe QUALQUER documento para a entidade (em
// andamento ou já finalizado), devolve o mesmo Id em vez de criar um duplicado — a tela de
// assinatura pode ser reaberta várias vezes para a mesma entidade (ex.: revisitar uma entrega de
// EPI já assinada) sem gerar uma segunda linha para a mesma (EntidadeTipo, EntidadeId), o que
// quebraria consultas que assumem um documento por entidade (ex.: ExportarFichaEpiTrabalhadorQuery).
public record CriarDocumentoAssinaturaCommand(string EntidadeTipo, Guid EntidadeId) : IRequest<Guid>;

public class CriarDocumentoAssinaturaCommandValidator : AbstractValidator<CriarDocumentoAssinaturaCommand>
{
    public CriarDocumentoAssinaturaCommandValidator()
    {
        RuleFor(x => x.EntidadeTipo).NotEmpty();
        RuleFor(x => x.EntidadeId).NotEmpty();
    }
}

public class CriarDocumentoAssinaturaCommandHandler : IRequestHandler<CriarDocumentoAssinaturaCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarDocumentoAssinaturaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarDocumentoAssinaturaCommand request, CancellationToken ct)
    {
        var existente = await _db.DocumentosAssinatura.FirstOrDefaultAsync(
            d => d.EntidadeTipo == request.EntidadeTipo && d.EntidadeId == request.EntidadeId,
            ct);
        if (existente is not null)
            return existente.Id;

        var documento = new DocumentoAssinatura
        {
            EntidadeTipo = request.EntidadeTipo,
            EntidadeId = request.EntidadeId,
        };
        _db.DocumentosAssinatura.Add(documento);
        await _db.SaveChangesAsync(ct);
        return documento.Id;
    }
}
