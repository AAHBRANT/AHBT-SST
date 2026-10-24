using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.TagsIdentificacao.Commands;

public record CriarTagIdentificacaoCommand(string Uid, TipoTag Tipo) : IRequest<Guid>;

public class CriarTagIdentificacaoCommandValidator : AbstractValidator<CriarTagIdentificacaoCommand>
{
    public CriarTagIdentificacaoCommandValidator()
    {
        RuleFor(x => x.Uid).NotEmpty().MaximumLength(100);
    }
}

public class CriarTagIdentificacaoCommandHandler : IRequestHandler<CriarTagIdentificacaoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarTagIdentificacaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarTagIdentificacaoCommand request, CancellationToken ct)
    {
        var jaExiste = await _db.TagsIdentificacao.AnyAsync(t => t.Uid == request.Uid, ct);
        if (jaExiste)
            throw new InvalidOperationException($"Já existe uma tag cadastrada com o UID {request.Uid}.");

        // O Uid tem índice único na tabela física, e uma tag excluída (soft-delete, Ativo = false)
        // continua ocupando essa linha — invisível para a checagem acima, mas ainda presente no
        // banco. Sem isto, o INSERT abaixo falhava com violação de chave única e o usuário só via um
        // erro genérico ao tentar recadastrar o mesmo UID físico (ex.: reemitir a tag de um
        // funcionário após excluir a anterior por engano). Reativamos a linha existente — preservando
        // Status/vínculo de antes da exclusão — em vez de tentar inserir uma duplicata.
        var tagExcluida = await _db.TagsIdentificacao.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Uid == request.Uid && !t.Ativo, ct);
        if (tagExcluida is not null)
        {
            tagExcluida.Ativo = true;
            tagExcluida.Tipo = request.Tipo;
            await _db.SaveChangesAsync(ct);
            return tagExcluida.Id;
        }

        var tag = new TagIdentificacao
        {
            Uid = request.Uid,
            Tipo = request.Tipo,
            Status = StatusTag.Disponivel
        };

        _db.TagsIdentificacao.Add(tag);
        await _db.SaveChangesAsync(ct);
        return tag.Id;
    }
}
