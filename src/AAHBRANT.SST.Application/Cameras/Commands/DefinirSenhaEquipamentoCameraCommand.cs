using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cameras.Commands;

// Grava (ou troca) a senha do equipamento, cifrada. É só escrita: nenhuma consulta devolve a senha ao front, apenas
// o indicador "senhaDefinida". Não há como apagar a senha: câmera IP precisa dela para ser ativada.
public record DefinirSenhaEquipamentoCameraCommand(Guid Id, string Senha) : IRequest
{
    public override string ToString() => $"{nameof(DefinirSenhaEquipamentoCameraCommand)} {{ Id = {Id} }}";
}

public class DefinirSenhaEquipamentoCameraCommandValidator : AbstractValidator<DefinirSenhaEquipamentoCameraCommand>
{
    public DefinirSenhaEquipamentoCameraCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Senha).NotEmpty().WithMessage("Informe a nova senha.")
            .MaximumLength(EquipamentoCameraRegras.TamanhoMaximoSenha)
            .WithMessage($"A senha pode ter no máximo {EquipamentoCameraRegras.TamanhoMaximoSenha} caracteres.");
    }
}

public class DefinirSenhaEquipamentoCameraCommandHandler : IRequestHandler<DefinirSenhaEquipamentoCameraCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICredencialCameraCriptografia _cripto;

    public DefinirSenhaEquipamentoCameraCommandHandler(IAppDbContext db, ICredencialCameraCriptografia cripto)
    {
        _db = db;
        _cripto = cripto;
    }

    public async Task Handle(DefinirSenhaEquipamentoCameraCommand request, CancellationToken ct)
    {
        var equipamento = await _db.EquipamentosCamera.FirstOrDefaultAsync(e => e.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Equipamento {request.Id} não encontrado.");

        equipamento.SenhaCifrada = _cripto.Cifrar(request.Senha);
        await _db.SaveChangesAsync(ct);
    }
}
