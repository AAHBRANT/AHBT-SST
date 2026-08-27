using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura.Commands;

public record CadastrarTemplateBiometricoCommand(Guid TrabalhadorId, byte[] TemplateBruto) : IRequest;

public class CadastrarTemplateBiometricoCommandValidator : AbstractValidator<CadastrarTemplateBiometricoCommand>
{
    public CadastrarTemplateBiometricoCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.TemplateBruto).NotEmpty();
    }
}

public class CadastrarTemplateBiometricoCommandHandler : IRequestHandler<CadastrarTemplateBiometricoCommand>
{
    private readonly IAppDbContext _db;
    private readonly ITemplateBiometricoCriptografia _criptografia;

    public CadastrarTemplateBiometricoCommandHandler(IAppDbContext db, ITemplateBiometricoCriptografia criptografia)
    {
        _db = db;
        _criptografia = criptografia;
    }

    public async Task Handle(CadastrarTemplateBiometricoCommand request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == request.TrabalhadorId, ct);
        if (trabalhador is null)
        {
            throw new KeyNotFoundException("Trabalhador não encontrado.");
        }

        if (trabalhador.TermoAceiteAssinaturaEletronicaEm is null || trabalhador.ConsentimentoBiometriaEm is null)
        {
            throw new InvalidOperationException("Trabalhador ainda não confirmou o Termo de Aceite ou o consentimento de biometria.");
        }

        var template = new TemplateBiometricoFutronic
        {
            TrabalhadorId = request.TrabalhadorId,
            TemplateCriptografado = _criptografia.Criptografar(request.TemplateBruto),
            CapturadoEm = DateTime.UtcNow,
        };
        _db.TemplatesBiometricoFutronic.Add(template);
        await _db.SaveChangesAsync(ct);
    }
}
