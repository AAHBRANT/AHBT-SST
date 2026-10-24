using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.PlanosIntegracao;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AAHBRANT.SST.Application.Trabalhadores.Commands;

public record ExcluirTrabalhadorCommand(Guid Id) : IRequest;

public class ExcluirTrabalhadorCommandValidator : AbstractValidator<ExcluirTrabalhadorCommand>
{
    public ExcluirTrabalhadorCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ExcluirTrabalhadorCommandHandler : IRequestHandler<ExcluirTrabalhadorCommand>
{
    private readonly IAppDbContext _db;
    private readonly IAutenticacaoFacialService? _facial;
    private readonly ILogger<ExcluirTrabalhadorCommandHandler> _logger;
    private readonly IPlanoIntegracaoService? _planoIntegracao;

    public ExcluirTrabalhadorCommandHandler(IAppDbContext db, IAutenticacaoFacialService? facial = null, ILogger<ExcluirTrabalhadorCommandHandler>? logger = null,
        IPlanoIntegracaoService? planoIntegracao = null)
    {
        _db = db;
        _facial = facial;
        _logger = logger ?? NullLogger<ExcluirTrabalhadorCommandHandler>.Instance;
        _planoIntegracao = planoIntegracao;
    }

    public async Task Handle(ExcluirTrabalhadorCommand request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Trabalhador {request.Id} não encontrado.");

        // Biometria primeiro (Azure + fotos/digitais arquivadas), depois o trabalhador. A exclusão é lógica
        // (SstDbContext converte Remove em Ativo = false); marcando direto, o EF não tenta "cortar" a relação com
        // as fotos de cadastro que acabaram de ser arquivadas neste mesmo contexto.
        await RemocaoBiometriaDesligamento.TentarAsync(_db, _facial, _logger, trabalhador.Id, ct);

        trabalhador.Ativo = false;
        await _db.SaveChangesAsync(ct);

        // Devolve ao disponível o kit de EPI que estava separado para ele.
        if (_planoIntegracao is not null)
            await _planoIntegracao.SincronizarAsync(trabalhador.Id, null, ct);
    }
}
