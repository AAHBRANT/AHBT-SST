using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Novidades.Commands;

public record CriarNovidadeVersaoCommand(
    string Titulo,
    string Versao,
    DateTime DataPublicacao,
    List<NovidadeVersaoItemInput> Itens) : IRequest<NovidadeVersaoDto>;

public class CriarNovidadeVersaoCommandValidator : AbstractValidator<CriarNovidadeVersaoCommand>
{
    public CriarNovidadeVersaoCommandValidator()
    {
        RuleFor(x => x.Titulo).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Versao).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Itens).NotEmpty().WithMessage("Cadastre pelo menos um item de novidade.");
        RuleForEach(x => x.Itens).ChildRules(item =>
        {
            item.RuleFor(i => i.Descricao).NotEmpty().MaximumLength(300);
            item.RuleFor(i => i.Antes).MaximumLength(1000);
            item.RuleFor(i => i.Agora).MaximumLength(1000);
        });
    }
}

public class CriarNovidadeVersaoCommandHandler : IRequestHandler<CriarNovidadeVersaoCommand, NovidadeVersaoDto>
{
    private readonly IAppDbContext _db;

    public CriarNovidadeVersaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<NovidadeVersaoDto> Handle(CriarNovidadeVersaoCommand request, CancellationToken ct)
    {
        var novidade = new NovidadeVersao
        {
            Titulo = request.Titulo.Trim(),
            Versao = request.Versao.Trim(),
            DataPublicacao = request.DataPublicacao,
        };

        var ordem = 0;
        foreach (var item in request.Itens)
        {
            novidade.Itens.Add(new NovidadeVersaoItem
            {
                Categoria = item.Categoria,
                Descricao = item.Descricao.Trim(),
                Antes = string.IsNullOrWhiteSpace(item.Antes) ? null : item.Antes.Trim(),
                Agora = string.IsNullOrWhiteSpace(item.Agora) ? null : item.Agora.Trim(),
                Ordem = ordem++,
            });
        }

        _db.NovidadesVersao.Add(novidade);
        await _db.SaveChangesAsync(ct);

        return NovidadesMapper.Mapear(novidade);
    }
}
