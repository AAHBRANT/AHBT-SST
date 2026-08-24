using AAHBRANT.SST.Application.AcoesPlano;
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.MatrizLegal.Queries;

// Agrega RequisitoLegal + AcaoPlano vinculados via query (padrão já usado em
// NaoConformidadeDetalheDto/AcidenteDetalheDto) — sem tabela de junção redundante.
public record ObterRequisitoLegalDetalheQuery(Guid Id) : IRequest<RequisitoLegalDetalheDto>;

public class ObterRequisitoLegalDetalheQueryHandler
    : IRequestHandler<ObterRequisitoLegalDetalheQuery, RequisitoLegalDetalheDto>
{
    private readonly IAppDbContext _db;

    public ObterRequisitoLegalDetalheQueryHandler(IAppDbContext db) => _db = db;

    public async Task<RequisitoLegalDetalheDto> Handle(ObterRequisitoLegalDetalheQuery request, CancellationToken ct)
    {
        var requisito = await _db.RequisitosLegais
            .Include(r => r.ResponsavelUsuario)
            .Include(r => r.Obra)
            .Where(r => r.Id == request.Id)
            .Select(r => new RequisitoLegalDto
            {
                Id = r.Id,
                Codigo = r.Codigo,
                Norma = r.Norma,
                Item = r.Item,
                Tema = r.Tema,
                Requisito = r.Requisito,
                Aplicabilidade = r.Aplicabilidade,
                Justificativa = r.Justificativa,
                Evidencia = r.Evidencia,
                ResponsavelUsuarioId = r.ResponsavelUsuarioId,
                ResponsavelUsuarioNome = r.ResponsavelUsuario != null ? r.ResponsavelUsuario.Nome : null,
                Periodicidade = r.Periodicidade,
                Prazo = r.Prazo,
                Status = r.Status,
                UltimaRevisao = r.UltimaRevisao,
                ProximaRevisao = r.ProximaRevisao,
                ObraId = r.ObraId,
                ObraNome = r.Obra != null ? r.Obra.Nome : null,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"Requisito legal {request.Id} não encontrado.");

        var acoesPlano = await _db.AcoesPlano
            .Where(a => a.OrigemTipo == nameof(Domain.Entidades.RequisitoLegal) && a.OrigemId == request.Id)
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

        return new RequisitoLegalDetalheDto
        {
            RequisitoLegal = requisito,
            AcoesPlano = acoesPlano,
        };
    }
}
