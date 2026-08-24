using AAHBRANT.SST.Application.AcoesPlano;
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.NaoConformidades.Queries;

// Agrega NaoConformidade + AcaoPlano vinculados via query (padrão já usado em InspecaoDetalheDto /
// PermissaoTrabalhoDetalheDto) — sem tabela de junção redundante.
public record ObterNaoConformidadeDetalheQuery(Guid Id) : IRequest<NaoConformidadeDetalheDto>;

public class ObterNaoConformidadeDetalheQueryHandler
    : IRequestHandler<ObterNaoConformidadeDetalheQuery, NaoConformidadeDetalheDto>
{
    private readonly IAppDbContext _db;

    public ObterNaoConformidadeDetalheQueryHandler(IAppDbContext db) => _db = db;

    public async Task<NaoConformidadeDetalheDto> Handle(ObterNaoConformidadeDetalheQuery request, CancellationToken ct)
    {
        var nc = await _db.NaoConformidades
            .Include(n => n.Atividade)
            .Include(n => n.ResponsavelUsuario)
            .Where(n => n.Id == request.Id)
            .Select(n => new NaoConformidadeDto
            {
                Id = n.Id,
                OrigemDeteccao = n.OrigemDeteccao,
                RequisitoRelacionado = n.RequisitoRelacionado,
                Descricao = n.Descricao,
                Local = n.Local,
                AtividadeId = n.AtividadeId,
                AtividadeNome = n.Atividade != null ? n.Atividade.Nome : null,
                RiscoId = n.RiscoId,
                ResponsavelUsuarioId = n.ResponsavelUsuarioId,
                ResponsavelUsuarioNome = n.ResponsavelUsuario != null ? n.ResponsavelUsuario.Nome : null,
                Prazo = n.Prazo,
                Status = n.Status,
                DataConclusao = n.DataConclusao,
                ObservacoesEncerramento = n.ObservacoesEncerramento,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"Não conformidade {request.Id} não encontrada.");

        var acoesPlano = await _db.AcoesPlano
            .Where(a => a.OrigemTipo == nameof(Domain.Entidades.NaoConformidade) && a.OrigemId == request.Id)
            .Include(a => a.ResponsavelUsuario)
            .Include(a => a.ValidadoPorUsuario)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new AcaoPlanoDto
            {
                Id = a.Id,
                OrigemTipo = a.OrigemTipo,
                OrigemId = a.OrigemId,
                Tipo = a.Tipo,
                Descricao = a.Descricao,
                ResponsavelUsuarioId = a.ResponsavelUsuarioId,
                ResponsavelUsuarioNome = a.ResponsavelUsuario != null ? a.ResponsavelUsuario.Nome : null,
                Prioridade = a.Prioridade,
                Prazo = a.Prazo,
                Status = a.Status,
                DataConclusao = a.DataConclusao,
                DataValidacao = a.DataValidacao,
                ValidadoPorUsuarioId = a.ValidadoPorUsuarioId,
                ValidadoPorUsuarioNome = a.ValidadoPorUsuario != null ? a.ValidadoPorUsuario.Nome : null,
            })
            .ToListAsync(ct);

        return new NaoConformidadeDetalheDto
        {
            NaoConformidade = nc,
            AcoesPlano = acoesPlano,
        };
    }
}
