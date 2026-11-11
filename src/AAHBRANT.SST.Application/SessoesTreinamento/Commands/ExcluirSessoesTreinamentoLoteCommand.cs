using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.SessoesTreinamento.Commands;

// Exclusão definitiva em lote (variação "seleção em massa" aprovada no mockup). Cada turma é avaliada e excluída
// sozinha: uma turma bloqueada (certificado, assinatura) não impede as outras. Devolve o resultado de cada uma.
public record ExcluirSessoesTreinamentoLoteCommand(IReadOnlyList<Guid> Ids) : IRequest<List<ResultadoExclusaoTurma>>;

public class ExcluirSessoesTreinamentoLoteCommandValidator : AbstractValidator<ExcluirSessoesTreinamentoLoteCommand>
{
    public const int MaximoPorLote = 100;

    public ExcluirSessoesTreinamentoLoteCommandValidator()
    {
        RuleFor(x => x.Ids).NotNull().NotEmpty().WithMessage("Selecione ao menos uma turma.");
        RuleFor(x => x.Ids).Must(i => i is null || i.Count <= MaximoPorLote)
            .WithMessage($"Selecione no máximo {MaximoPorLote} turmas por vez.");
        RuleForEach(x => x.Ids).NotEmpty();
    }
}

public class ExcluirSessoesTreinamentoLoteCommandHandler : IRequestHandler<ExcluirSessoesTreinamentoLoteCommand, List<ResultadoExclusaoTurma>>
{
    private readonly IAppDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly ICurrentUserService _usuario;

    public ExcluirSessoesTreinamentoLoteCommandHandler(IAppDbContext db, IAuditoriaService auditoria, ICurrentUserService usuario)
    {
        _db = db;
        _auditoria = auditoria;
        _usuario = usuario;
    }

    public async Task<List<ResultadoExclusaoTurma>> Handle(ExcluirSessoesTreinamentoLoteCommand request, CancellationToken ct)
    {
        var resultados = new List<ResultadoExclusaoTurma>();
        foreach (var id in request.Ids.Distinct())
            resultados.Add(await ExclusaoSessaoTreinamento.ExecutarAsync(_db, _auditoria, _usuario.UsuarioId, id, ct));
        return resultados;
    }
}
