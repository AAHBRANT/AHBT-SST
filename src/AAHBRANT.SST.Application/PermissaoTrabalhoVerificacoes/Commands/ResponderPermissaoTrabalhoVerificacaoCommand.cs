using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissaoTrabalhoVerificacoes.Commands;

// §4 do formulário — cada PT já nasce com os 15 itens fixos (CriarPermissaoTrabalhoCommand); este
// comando só registra a resposta C/NC/NA, não cria/exclui linha. Qualquer NaoConforme bloqueia a
// liberação (ver AutorizarPermissaoTrabalhoCommand).
public record ResponderPermissaoTrabalhoVerificacaoCommand(Guid Id, RespostaVerificacaoPt Resposta) : IRequest;

public class ResponderPermissaoTrabalhoVerificacaoCommandValidator : AbstractValidator<ResponderPermissaoTrabalhoVerificacaoCommand>
{
    public ResponderPermissaoTrabalhoVerificacaoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Resposta).IsInEnum();
    }
}

public class ResponderPermissaoTrabalhoVerificacaoCommandHandler : IRequestHandler<ResponderPermissaoTrabalhoVerificacaoCommand>
{
    private readonly IAppDbContext _db;

    public ResponderPermissaoTrabalhoVerificacaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ResponderPermissaoTrabalhoVerificacaoCommand request, CancellationToken ct)
    {
        var item = await _db.PermissaoTrabalhoVerificacoes.FirstOrDefaultAsync(v => v.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Verificação {request.Id} não encontrada.");

        item.Resposta = request.Resposta;
        await _db.SaveChangesAsync(ct);
    }
}
