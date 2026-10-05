using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Plataforma;

// Decisão de arquitetura da Fase 1 (docs/qualidade): identidade e Obra existentes,
// sem duplicação de cadastro, tabelas ou concessão automática de permissões.
public static class ModulosPlataforma
{
    public const string QualidadeVer = "qualidade:ver";
    public const string TeamsConfigurar = "teams:configurar";

    public static string PermissaoLeitura(string modulo) => modulo switch
    {
        "sst" => "organizacional:ver",
        "qualidade" => QualidadeVer,
        _ => throw new InvalidOperationException("Módulo inválido."),
    };
}

public sealed record ObraModuloDto(Guid Id, string Codigo, string Nome, string? Cidade, string? Uf, StatusObra Status);
public sealed record ListarObrasModuloQuery(string? ObjectId, string Modulo, bool ConfigurarTeams = false)
    : IRequest<IReadOnlyList<ObraModuloDto>>;

public sealed class ListarObrasModuloHandler(IAppDbContext db, IAcessoPorObraService acesso)
    : IRequestHandler<ListarObrasModuloQuery, IReadOnlyList<ObraModuloDto>>
{
    public async Task<IReadOnlyList<ObraModuloDto>> Handle(ListarObrasModuloQuery request, CancellationToken ct)
    {
        var escopo = await acesso.ObterEscopoAsync(request.ObjectId, ModulosPlataforma.PermissaoLeitura(request.Modulo), ct);
        if (request.ConfigurarTeams)
            escopo = escopo.Intersectar(await acesso.ObterEscopoAsync(request.ObjectId, ModulosPlataforma.TeamsConfigurar, ct));
        if (!escopo.TemPermissao) throw new AcessoNegadoException();

        var obras = escopo.Obras.ToArray();
        return await db.Obras.AsNoTracking().Where(o => o.Ativo && (escopo.Global || obras.Contains(o.Id)))
            .OrderBy(o => o.Nome).ThenBy(o => o.Id)
            .Select(o => new ObraModuloDto(o.Id, o.Codigo, o.Nome, o.Cidade, o.Uf, o.Status)).ToListAsync(ct);
    }
}

public sealed record ContextoModuloDto(ObraModuloDto Obra, bool PodeConfigurarTeams);
public sealed record ObterContextoModuloQuery(string? ObjectId, string Modulo, Guid ObraId, bool ConfigurarTeams = false)
    : IRequest<ContextoModuloDto>;

public sealed class ObterContextoModuloHandler(IAppDbContext db, IAcessoPorObraService acesso)
    : IRequestHandler<ObterContextoModuloQuery, ContextoModuloDto>
{
    public async Task<ContextoModuloDto> Handle(ObterContextoModuloQuery request, CancellationToken ct)
    {
        var escopo = await acesso.ObterEscopoAsync(request.ObjectId, ModulosPlataforma.PermissaoLeitura(request.Modulo), ct);
        if (!escopo.Permite(request.ObraId)) throw new AcessoNegadoException();
        var podeConfigurar = (await acesso.ObterEscopoAsync(request.ObjectId, ModulosPlataforma.TeamsConfigurar, ct)).Permite(request.ObraId);
        if (request.ConfigurarTeams && !podeConfigurar) throw new AcessoNegadoException();

        var obra = await db.Obras.AsNoTracking().Where(o => o.Id == request.ObraId && o.Ativo)
            .Select(o => new ObraModuloDto(o.Id, o.Codigo, o.Nome, o.Cidade, o.Uf, o.Status)).SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Obra não encontrada.");
        return new(obra, podeConfigurar);
    }
}
