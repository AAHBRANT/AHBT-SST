using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PerfisAcesso.Commands;

// Endpoint da "Matriz de Permissões": substitui de uma vez todas as concessões do perfil
// pela lista enviada (grid de checkboxes Permissao x Escopo no frontend salva em um único POST).
// Dupla validação do usuário (Módulo+Ação, aqui = Permissao.Codigo, E Escopo) é o que cada
// linha desta matriz representa; o enforcement real por requisição fica no IAuthorizationHandler
// de obra-escopo (docs/RBAC-Matrix.md §4), que ainda depende do Entra ID SSO estar provisionado.
public record ItemPermissaoPerfil(Guid PermissaoId, EscopoAcesso Escopo, bool Permitido);

public record DefinirPermissoesPerfilCommand(
    Guid PerfilAcessoId,
    List<ItemPermissaoPerfil> Itens) : IRequest;

public class DefinirPermissoesPerfilCommandValidator : AbstractValidator<DefinirPermissoesPerfilCommand>
{
    public DefinirPermissoesPerfilCommandValidator()
    {
        RuleFor(x => x.PerfilAcessoId).NotEmpty();
        RuleForEach(x => x.Itens).ChildRules(item =>
        {
            item.RuleFor(i => i.PermissaoId).NotEmpty();
        });
    }
}

public class DefinirPermissoesPerfilCommandHandler : IRequestHandler<DefinirPermissoesPerfilCommand>
{
    private readonly IAppDbContext _db;

    public DefinirPermissoesPerfilCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DefinirPermissoesPerfilCommand request, CancellationToken ct)
    {
        var perfilExiste = await _db.PerfisAcesso.AnyAsync(p => p.Id == request.PerfilAcessoId, ct);
        if (!perfilExiste)
            throw new KeyNotFoundException($"Perfil de acesso {request.PerfilAcessoId} não encontrado.");

        // IgnoreQueryFilters: RemoveRange nunca exclui fisicamente (AplicarAuditoria em
        // SstDbContext converte em soft-delete), então uma linha já salva antes continua
        // ocupando o índice único mesmo com Ativo=false. Sem enxergar essas linhas aqui,
        // reenviar uma permissão já concedida (o normal no "substituir tudo" da matriz)
        // tentaria inserir de novo a mesma combinação e violaria o índice único.
        var existentes = await _db.PerfisAcessoPermissoes
            .IgnoreQueryFilters()
            .Where(pp => pp.PerfilAcessoId == request.PerfilAcessoId)
            .ToListAsync(ct);

        var existentesPorChave = existentes.ToDictionary(pp => (pp.PermissaoId, pp.Escopo));
        var chavesRecebidas = request.Itens.Select(i => (i.PermissaoId, i.Escopo)).ToHashSet();

        foreach (var item in request.Itens)
        {
            if (existentesPorChave.TryGetValue((item.PermissaoId, item.Escopo), out var existente))
            {
                existente.Permitido = item.Permitido;
                existente.Ativo = true;
            }
            else
            {
                _db.PerfisAcessoPermissoes.Add(new PerfilAcessoPermissao
                {
                    PerfilAcessoId = request.PerfilAcessoId,
                    PermissaoId = item.PermissaoId,
                    Escopo = item.Escopo,
                    Permitido = item.Permitido
                });
            }
        }

        var paraRemover = existentes.Where(pp => pp.Ativo && !chavesRecebidas.Contains((pp.PermissaoId, pp.Escopo)));
        _db.PerfisAcessoPermissoes.RemoveRange(paraRemover);

        await _db.SaveChangesAsync(ct);
    }
}
