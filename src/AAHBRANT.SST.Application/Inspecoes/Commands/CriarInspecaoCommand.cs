using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Commands;

// Criar uma Inspecao a partir de um ChecklistModelo já gera uma InspecaoItemResposta "em branco"
// (StatusItem=null) por item vigente do checklist, para que a execução vire só preencher cada
// linha (ver disclosure em Inspecao.cs sobre o nullable de StatusItem).
public record CriarInspecaoCommand(
    Guid ChecklistModeloId,
    Guid ObraId,
    Guid? AtividadeId,
    DateTime Data,
    Guid ResponsavelUsuarioId) : IRequest<Guid>;

public class CriarInspecaoCommandValidator : AbstractValidator<CriarInspecaoCommand>
{
    public CriarInspecaoCommandValidator()
    {
        RuleFor(x => x.ChecklistModeloId).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.ResponsavelUsuarioId).NotEmpty();
    }
}

public class CriarInspecaoCommandHandler : IRequestHandler<CriarInspecaoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarInspecaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarInspecaoCommand request, CancellationToken ct)
    {
        var checklist = await _db.ChecklistModelos
            .Include(c => c.Itens)
            .FirstOrDefaultAsync(c => c.Id == request.ChecklistModeloId, ct)
            ?? throw new KeyNotFoundException($"Checklist {request.ChecklistModeloId} não encontrado.");

        var obraExiste = await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct);
        if (!obraExiste)
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var usuarioExiste = await _db.Usuarios.AnyAsync(u => u.Id == request.ResponsavelUsuarioId, ct);
        if (!usuarioExiste)
            throw new KeyNotFoundException($"Usuário {request.ResponsavelUsuarioId} não encontrado.");

        var inspecao = new Inspecao
        {
            TipoInspecao = checklist.TipoInspecao,
            ObraId = request.ObraId,
            AtividadeId = request.AtividadeId,
            ChecklistModeloId = checklist.Id,
            Data = request.Data,
            ResponsavelUsuarioId = request.ResponsavelUsuarioId,
        };

        foreach (var item in checklist.Itens.Where(i => i.Ativo))
        {
            inspecao.Respostas.Add(new InspecaoItemResposta
            {
                ChecklistModeloItemId = item.Id,
            });
        }

        _db.Inspecoes.Add(inspecao);
        await _db.SaveChangesAsync(ct);
        return inspecao.Id;
    }
}
