using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Empresas.Commands;

public record AtualizarEmpresaCommand(
    Guid Id,
    string RazaoSocial,
    string? NomeFantasia,
    string? TipoServicoPrestado,
    string? ContatoNome,
    string? ContatoTelefone,
    string? ContatoEmail,
    StatusEmpresa Status) : IRequest;

public class AtualizarEmpresaCommandValidator : AbstractValidator<AtualizarEmpresaCommand>
{
    public AtualizarEmpresaCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.RazaoSocial).NotEmpty().MaximumLength(200);
    }
}

public class AtualizarEmpresaCommandHandler : IRequestHandler<AtualizarEmpresaCommand>
{
    private readonly IAppDbContext _db;
    public AtualizarEmpresaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarEmpresaCommand request, CancellationToken ct)
    {
        var empresa = await _db.Empresas.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Empresa não encontrada.");

        empresa.RazaoSocial = request.RazaoSocial;
        empresa.NomeFantasia = request.NomeFantasia;
        empresa.TipoServicoPrestado = request.TipoServicoPrestado;
        empresa.ContatoNome = request.ContatoNome;
        empresa.ContatoTelefone = request.ContatoTelefone;
        empresa.ContatoEmail = request.ContatoEmail;
        empresa.Status = request.Status;

        await _db.SaveChangesAsync(ct);
    }
}
