using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura.Queries;

public record TemplateSincronizadoDto(Guid TrabalhadorId, string TrabalhadorNome, string TemplateCriptografado);

public record SincronizarTemplatesQuery(Guid DispositivoId, string SegredoDispositivo) : IRequest<List<TemplateSincronizadoDto>>;

public class SincronizarTemplatesQueryValidator : AbstractValidator<SincronizarTemplatesQuery>
{
    public SincronizarTemplatesQueryValidator()
    {
        RuleFor(x => x.DispositivoId).NotEmpty();
        RuleFor(x => x.SegredoDispositivo).NotEmpty();
    }
}

public class SincronizarTemplatesQueryHandler : IRequestHandler<SincronizarTemplatesQuery, List<TemplateSincronizadoDto>>
{
    private readonly IAppDbContext _db;
    private readonly IDispositivoAgenteAutenticador _dispositivoAutenticador;

    public SincronizarTemplatesQueryHandler(IAppDbContext db, IDispositivoAgenteAutenticador dispositivoAutenticador)
    {
        _db = db;
        _dispositivoAutenticador = dispositivoAutenticador;
    }

    public async Task<List<TemplateSincronizadoDto>> Handle(SincronizarTemplatesQuery request, CancellationToken ct)
    {
        var dispositivo = await _dispositivoAutenticador.ValidarAsync(request.DispositivoId, request.SegredoDispositivo, ct);

        dispositivo.UltimaSincronizacaoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Rota anônima (agente local, sem usuário): o filtro global de RBAC do Trabalhador esconderia todos
        // os trabalhadores. O acesso já foi validado pelo segredo do dispositivo, então o filtro é reaplicado
        // à mão (soft delete + obra do posto) e o JOIN é feito explicitamente.
        var trabalhadoresDaObra = _db.Trabalhadores
            .IgnoreQueryFilters()
            .Where(t => t.Ativo && t.ObraId == dispositivo.ObraId);

        return await _db.TemplatesBiometricoFutronic
            .Join(trabalhadoresDaObra, t => t.TrabalhadorId, tr => tr.Id,
                (t, tr) => new TemplateSincronizadoDto(tr.Id, tr.Nome, t.TemplateCriptografado))
            .ToListAsync(ct);
    }
}
