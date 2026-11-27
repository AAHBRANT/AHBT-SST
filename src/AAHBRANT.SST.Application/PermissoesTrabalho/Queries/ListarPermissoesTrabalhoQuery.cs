using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissoesTrabalho.Queries;

public record ListarPermissoesTrabalhoQuery(Guid? AtividadeId = null) : IRequest<List<PermissaoTrabalhoDto>>;

public class ListarPermissoesTrabalhoQueryHandler : IRequestHandler<ListarPermissoesTrabalhoQuery, List<PermissaoTrabalhoDto>>
{
    private readonly IAppDbContext _db;

    public ListarPermissoesTrabalhoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<PermissaoTrabalhoDto>> Handle(ListarPermissoesTrabalhoQuery request, CancellationToken ct)
    {
        var query = _db.PermissoesTrabalho
            .NoEscopoDaAtividade(_db, p => p.AtividadeId)
            .Include(p => p.Atividade!).ThenInclude(a => a.Obra)
            .Include(p => p.Equipe)
            .Include(p => p.AutorizadoPorUsuario)
            .Include(p => p.EncerradaPorUsuario)
            .AsQueryable();

        if (request.AtividadeId.HasValue)
            query = query.Where(p => p.AtividadeId == request.AtividadeId.Value);

        var permissoes = await query.OrderByDescending(p => p.CreatedAtUtc).ToListAsync(ct);
        // Responsável pela execução com nome e foto na lista (25/11/2026), sem carregar as imagens.
        var pessoas = await FotosPessoas.DeUsuariosAsync(_db, permissoes.Select(p => p.ResponsavelExecucaoUsuarioId), ct);
        PessoaComFoto? Pessoa(Guid? id) => id.HasValue && pessoas.TryGetValue(id.Value, out var p) ? p : null;

        return permissoes.Select(p => new PermissaoTrabalhoDto
        {
            Id = p.Id,
            NumeroPt = p.NumeroPt,
            AtividadeId = p.AtividadeId,
            AtividadeNome = p.Atividade?.Nome ?? string.Empty,
            ObraNome = p.Atividade?.Obra?.Nome,
            DescricaoAtividade = p.DescricaoAtividade,
            Local = p.Local,
            EmpresaExecutante = p.EmpresaExecutante,
            EquipeId = p.EquipeId,
            EquipeNome = p.Equipe?.Nome,
            Data = p.Data,
            HorarioInicio = p.HorarioInicio,
            HorarioFim = p.HorarioFim,
            Validade = p.Validade,
            ResponsavelExecucaoUsuarioId = p.ResponsavelExecucaoUsuarioId,
            ResponsavelExecucaoUsuarioNome = Pessoa(p.ResponsavelExecucaoUsuarioId)?.Nome,
            ResponsavelExecucaoTrabalhadorId = Pessoa(p.ResponsavelExecucaoUsuarioId)?.TrabalhadorId,
            ResponsavelExecucaoTemFoto = Pessoa(p.ResponsavelExecucaoUsuarioId)?.TemFoto ?? false,
            Status = p.Status,
            AutorizadoPorUsuarioId = p.AutorizadoPorUsuarioId,
            AutorizadoPorUsuarioNome = p.AutorizadoPorUsuario?.Nome,
            DataAutorizacao = p.DataAutorizacao,
            EncerradaPorUsuarioId = p.EncerradaPorUsuarioId,
            EncerradaPorUsuarioNome = p.EncerradaPorUsuario?.Nome,
            DataEncerramento = p.DataEncerramento,
            ObservacoesEncerramento = p.ObservacoesEncerramento
        }).ToList();
    }
}
