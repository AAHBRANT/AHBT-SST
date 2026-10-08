using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura.Commands;

public record CadastrarTemplateBiometricoCommand(Guid TrabalhadorId, byte[] TemplateBruto, byte[]? ImagemCadastroPng = null) : IRequest;

public class CadastrarTemplateBiometricoCommandValidator : AbstractValidator<CadastrarTemplateBiometricoCommand>
{
    public CadastrarTemplateBiometricoCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.TemplateBruto).NotEmpty();
        // PNG da leitura do cadastro (320x480 em tons de cinza fica bem abaixo de 1 MB).
        RuleFor(x => x.ImagemCadastroPng).Must(i => i is null || i.Length <= 1_000_000)
            .WithMessage("Imagem da digital maior que o permitido.");
    }
}

public class CadastrarTemplateBiometricoCommandHandler : IRequestHandler<CadastrarTemplateBiometricoCommand>
{
    private readonly IAppDbContext _db;
    private readonly ITemplateBiometricoCriptografia _criptografia;
    private readonly IImagemBiometricaCriptografia? _criptografiaImagem;

    public CadastrarTemplateBiometricoCommandHandler(
        IAppDbContext db, ITemplateBiometricoCriptografia criptografia, IImagemBiometricaCriptografia? criptografiaImagem = null)
    {
        _db = db;
        _criptografia = criptografia;
        _criptografiaImagem = criptografiaImagem;
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
            throw new InvalidOperationException("Trabalhador ainda não confirmou o Termo de Aceite de Assinatura Eletrônica e o consentimento LGPD para uso de biometria.");
        }

        // Regra do usuário (30/09): a digital é cadastrada uma única vez. A tela já esconde o botão
        // depois do sucesso; a trava aqui cobre aba antiga aberta, duplo clique e chamada direta.
        if (await _db.TemplatesBiometricoFutronic.AnyAsync(tb => tb.TrabalhadorId == request.TrabalhadorId, ct))
        {
            throw new InvalidOperationException("A digital deste trabalhador já está cadastrada.");
        }

        var template = new TemplateBiometricoFutronic
        {
            TrabalhadorId = request.TrabalhadorId,
            TemplateCriptografado = _criptografia.Criptografar(request.TemplateBruto),
            CapturadoEm = DateTime.UtcNow,
            ImagemCadastroCriptografada = request.ImagemCadastroPng is { Length: > 0 } && _criptografiaImagem is not null
                ? _criptografiaImagem.Criptografar(request.ImagemCadastroPng)
                : null,
        };
        _db.TemplatesBiometricoFutronic.Add(template);
        await _db.SaveChangesAsync(ct);
    }
}
