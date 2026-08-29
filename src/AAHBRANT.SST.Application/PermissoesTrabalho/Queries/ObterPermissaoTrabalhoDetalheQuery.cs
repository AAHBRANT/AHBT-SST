using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissoesTrabalho.Queries;

public record ObterPermissaoTrabalhoDetalheQuery(Guid Id) : IRequest<PermissaoTrabalhoDetalheDto?>;

public class ObterPermissaoTrabalhoDetalheQueryHandler : IRequestHandler<ObterPermissaoTrabalhoDetalheQuery, PermissaoTrabalhoDetalheDto?>
{
    private readonly IAppDbContext _db;

    public ObterPermissaoTrabalhoDetalheQueryHandler(IAppDbContext db) => _db = db;

    public async Task<PermissaoTrabalhoDetalheDto?> Handle(ObterPermissaoTrabalhoDetalheQuery request, CancellationToken ct)
    {
        var pt = await _db.PermissoesTrabalho
            .Include(p => p.Atividade!).ThenInclude(a => a.Obra)
            .Include(p => p.Equipe)
            .Include(p => p.ResponsavelExecucaoUsuario)
            .Include(p => p.ResponsavelAreaUsuario)
            .Include(p => p.AutorizadoPorUsuario)
            .Include(p => p.ResponsavelSstUsuario)
            .Include(p => p.SuspensaPorUsuario)
            .Include(p => p.RevalidadaPorUsuario)
            .Include(p => p.EncerradaPorUsuario)
            .FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        if (pt is null) return null;

        var preRequisitos = await _db.PermissaoTrabalhoPreRequisitos
            .Where(r => r.PermissaoTrabalhoId == pt.Id).OrderBy(r => r.Item).ToListAsync(ct);
        var tiposTrabalho = await _db.PermissaoTrabalhoTiposTrabalho
            .Where(t => t.PermissaoTrabalhoId == pt.Id).OrderBy(t => t.Tipo).ToListAsync(ct);
        var verificacoes = await _db.PermissaoTrabalhoVerificacoes
            .Where(v => v.PermissaoTrabalhoId == pt.Id).OrderBy(v => v.Item).ToListAsync(ct);
        var epis = await _db.PermissaoTrabalhoEpis
            .Where(e => e.PermissaoTrabalhoId == pt.Id).OrderBy(e => e.Item).ToListAsync(ct);
        var epcs = await _db.PermissaoTrabalhoEpcs
            .Where(e => e.PermissaoTrabalhoId == pt.Id).OrderBy(e => e.Item).ToListAsync(ct);
        var riscosCriticos = await _db.PermissaoTrabalhoRiscosCriticos
            .Where(r => r.PermissaoTrabalhoId == pt.Id).ToListAsync(ct);
        var responsaveis = await _db.PermissaoTrabalhoResponsaveis
            .Where(r => r.PermissaoTrabalhoId == pt.Id)
            .Include(r => r.Trabalhador!).ThenInclude(t => t.Funcao)
            .ToListAsync(ct);

        return new PermissaoTrabalhoDetalheDto
        {
            PermissaoTrabalho = new PermissaoTrabalhoDto
            {
                Id = pt.Id,
                NumeroPt = pt.NumeroPt,
                AtividadeId = pt.AtividadeId,
                AtividadeNome = pt.Atividade?.Nome ?? string.Empty,
                ObraNome = pt.Atividade?.Obra?.Nome,
                DescricaoAtividade = pt.DescricaoAtividade,
                Local = pt.Local,
                EmpresaExecutante = pt.EmpresaExecutante,
                EquipeId = pt.EquipeId,
                EquipeNome = pt.Equipe?.Nome,
                Data = pt.Data,
                HorarioInicio = pt.HorarioInicio,
                HorarioFim = pt.HorarioFim,
                Validade = pt.Validade,
                ResponsavelExecucaoUsuarioId = pt.ResponsavelExecucaoUsuarioId,
                ResponsavelExecucaoUsuarioNome = pt.ResponsavelExecucaoUsuario?.Nome,
                ResponsavelAreaUsuarioId = pt.ResponsavelAreaUsuarioId,
                ResponsavelAreaUsuarioNome = pt.ResponsavelAreaUsuario?.Nome,
                Status = pt.Status,
                AutorizadoPorUsuarioId = pt.AutorizadoPorUsuarioId,
                AutorizadoPorUsuarioNome = pt.AutorizadoPorUsuario?.Nome,
                DataAutorizacao = pt.DataAutorizacao,
                DataAssinaturaExecucao = pt.DataAssinaturaExecucao,
                ResponsavelSstUsuarioId = pt.ResponsavelSstUsuarioId,
                ResponsavelSstUsuarioNome = pt.ResponsavelSstUsuario?.Nome,
                DataAssinaturaSst = pt.DataAssinaturaSst,
                SuspensaPorUsuarioId = pt.SuspensaPorUsuarioId,
                SuspensaPorUsuarioNome = pt.SuspensaPorUsuario?.Nome,
                DataSuspensao = pt.DataSuspensao,
                MotivoSuspensao = pt.MotivoSuspensao,
                RevalidadaPorUsuarioId = pt.RevalidadaPorUsuarioId,
                RevalidadaPorUsuarioNome = pt.RevalidadaPorUsuario?.Nome,
                DataRevalidacao = pt.DataRevalidacao,
                EncerradaPorUsuarioId = pt.EncerradaPorUsuarioId,
                EncerradaPorUsuarioNome = pt.EncerradaPorUsuario?.Nome,
                DataEncerramento = pt.DataEncerramento,
                ObservacoesEncerramento = pt.ObservacoesEncerramento,
                OutrosEpis = pt.OutrosEpis,
                OutrosEpcs = pt.OutrosEpcs,
            },
            PreRequisitos = preRequisitos.Select(r => new PermissaoTrabalhoPreRequisitoDto
            {
                Id = r.Id, PermissaoTrabalhoId = r.PermissaoTrabalhoId, Item = r.Item, Atendido = r.Atendido
            }).ToList(),
            TiposTrabalho = tiposTrabalho.Select(t => new PermissaoTrabalhoTipoTrabalhoDto
            {
                Id = t.Id, PermissaoTrabalhoId = t.PermissaoTrabalhoId, Tipo = t.Tipo, DescricaoOutro = t.DescricaoOutro
            }).ToList(),
            Verificacoes = verificacoes.Select(v => new PermissaoTrabalhoVerificacaoDto
            {
                Id = v.Id, PermissaoTrabalhoId = v.PermissaoTrabalhoId, Item = v.Item, Resposta = v.Resposta
            }).ToList(),
            Epis = epis.Select(e => new PermissaoTrabalhoEpiDto
            {
                Id = e.Id, PermissaoTrabalhoId = e.PermissaoTrabalhoId, Item = e.Item, Complemento = e.Complemento
            }).ToList(),
            Epcs = epcs.Select(e => new PermissaoTrabalhoEpcDto
            {
                Id = e.Id, PermissaoTrabalhoId = e.PermissaoTrabalhoId, Item = e.Item
            }).ToList(),
            RiscosCriticos = riscosCriticos.Select(r => new PermissaoTrabalhoRiscoCriticoDto
            {
                Id = r.Id, PermissaoTrabalhoId = r.PermissaoTrabalhoId, RiscoCondicao = r.RiscoCondicao,
                ControleComplementar = r.ControleComplementar, ResponsavelEvidencia = r.ResponsavelEvidencia
            }).ToList(),
            Responsaveis = responsaveis.Select(r => new PermissaoTrabalhoResponsavelDto
            {
                Id = r.Id, PermissaoTrabalhoId = r.PermissaoTrabalhoId, TrabalhadorId = r.TrabalhadorId,
                TrabalhadorNome = r.Trabalhador?.Nome ?? string.Empty, TrabalhadorFuncaoNome = r.Trabalhador?.Funcao?.Nome
            }).ToList()
        };
    }
}
