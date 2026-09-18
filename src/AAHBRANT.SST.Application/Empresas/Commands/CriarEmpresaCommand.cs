using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Empresas.Commands;

public record CriarEmpresaCommand(
    string RazaoSocial,
    string? NomeFantasia,
    string Cnpj,
    string? TipoServicoPrestado,
    string? ContatoNome,
    string? ContatoTelefone,
    string? ContatoEmail) : IRequest<Guid>;

public class CriarEmpresaCommandValidator : AbstractValidator<CriarEmpresaCommand>
{
    public CriarEmpresaCommandValidator()
    {
        RuleFor(x => x.RazaoSocial).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Cnpj).NotEmpty().Length(14).Matches("^[0-9]+$").WithMessage("Informe o CNPJ com 14 dígitos, sem pontuação.");
    }
}

public class CriarEmpresaCommandHandler : IRequestHandler<CriarEmpresaCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CriarEmpresaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarEmpresaCommand request, CancellationToken ct)
    {
        var cnpjEmUso = await _db.Empresas.AnyAsync(e => e.Cnpj == request.Cnpj, ct);
        if (cnpjEmUso)
            throw new InvalidOperationException("Já existe uma empresa cadastrada com este CNPJ.");

        var empresa = new Empresa
        {
            RazaoSocial = request.RazaoSocial,
            NomeFantasia = request.NomeFantasia,
            Cnpj = request.Cnpj,
            TipoServicoPrestado = request.TipoServicoPrestado,
            ContatoNome = request.ContatoNome,
            ContatoTelefone = request.ContatoTelefone,
            ContatoEmail = request.ContatoEmail,
        };
        _db.Empresas.Add(empresa);
        await _db.SaveChangesAsync(ct);
        return empresa.Id;
    }
}
