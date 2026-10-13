using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Assinatura.Queries;

// Dos registros de uma lista (ex.: entregas de EPI da tela), quais já estão travados pela assinatura — a tela
// usa para esconder os botões de editar/excluir. A regra é a mesma dos comandos (ProtecaoDocumentoAssinado).
public record ListarRegistrosTravadosQuery(string EntidadeTipo, List<Guid> Ids) : IRequest<List<Guid>>;

public class ListarRegistrosTravadosQueryValidator : AbstractValidator<ListarRegistrosTravadosQuery>
{
    public ListarRegistrosTravadosQueryValidator()
    {
        RuleFor(x => x.EntidadeTipo).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Ids).NotNull().Must(ids => ids.Count <= 1000).WithMessage("Informe no máximo 1000 registros.");
    }
}

public class ListarRegistrosTravadosQueryHandler : IRequestHandler<ListarRegistrosTravadosQuery, List<Guid>>
{
    private readonly IAppDbContext _db;

    public ListarRegistrosTravadosQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<Guid>> Handle(ListarRegistrosTravadosQuery request, CancellationToken ct)
        => (await ProtecaoDocumentoAssinado.ListarTravadosAsync(_db, request.EntidadeTipo, request.Ids.Distinct().ToList(), ct)).ToList();
}
