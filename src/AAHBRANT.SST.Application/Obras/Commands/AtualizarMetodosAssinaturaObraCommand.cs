using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Obras.Commands;

// Liga/desliga, por obra, os métodos de assinatura eletrônica aceitos (Obra.MetodosAutenticacaoHabilitados).
// Comando separado de AtualizarObraCommand de propósito: é configuração de segurança/jurídico, não dado
// cadastral, e não deve ser sobrescrita por uma edição comum da obra.
public record AtualizarMetodosAssinaturaObraCommand(Guid Id, bool Biometria, bool ReconhecimentoFacial) : IRequest;

public class AtualizarMetodosAssinaturaObraCommandValidator : AbstractValidator<AtualizarMetodosAssinaturaObraCommand>
{
    public AtualizarMetodosAssinaturaObraCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class AtualizarMetodosAssinaturaObraCommandHandler : IRequestHandler<AtualizarMetodosAssinaturaObraCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarMetodosAssinaturaObraCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarMetodosAssinaturaObraCommand request, CancellationToken ct)
    {
        var obra = await _db.Obras.FirstOrDefaultAsync(o => o.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Obra {request.Id} não encontrada.");

        var metodos = MetodoAutenticacaoObra.Nenhum;
        if (request.Biometria) metodos |= MetodoAutenticacaoObra.Biometria;
        if (request.ReconhecimentoFacial) metodos |= MetodoAutenticacaoObra.ReconhecimentoFacial;

        obra.MetodosAutenticacaoHabilitados = metodos;
        await _db.SaveChangesAsync(ct);
    }
}
