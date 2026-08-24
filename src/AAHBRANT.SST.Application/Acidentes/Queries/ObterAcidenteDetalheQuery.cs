using AAHBRANT.SST.Application.AcoesPlano;
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Acidentes.Queries;

// Agrega Acidente + AcaoPlano vinculados via query (padrão já usado em NaoConformidadeDetalheDto) —
// sem tabela de junção redundante.
public record ObterAcidenteDetalheQuery(Guid Id) : IRequest<AcidenteDetalheDto>;

public class ObterAcidenteDetalheQueryHandler : IRequestHandler<ObterAcidenteDetalheQuery, AcidenteDetalheDto>
{
    private readonly IAppDbContext _db;

    public ObterAcidenteDetalheQueryHandler(IAppDbContext db) => _db = db;

    public async Task<AcidenteDetalheDto> Handle(ObterAcidenteDetalheQuery request, CancellationToken ct)
    {
        var acidente = await _db.Acidentes
            .Include(a => a.Obra)
            .Include(a => a.Trabalhador)
            .Include(a => a.Atividade)
            .Where(a => a.Id == request.Id)
            .Select(a => new AcidenteDto
            {
                Id = a.Id,
                Tipo = a.Tipo,
                ObraId = a.ObraId,
                ObraNome = a.Obra != null ? a.Obra.Nome : null,
                TrabalhadorId = a.TrabalhadorId,
                TrabalhadorNome = a.Trabalhador != null ? a.Trabalhador.Nome : null,
                AtividadeId = a.AtividadeId,
                AtividadeNome = a.Atividade != null ? a.Atividade.Nome : null,
                Local = a.Local,
                Data = a.Data,
                Hora = a.Hora,
                Descricao = a.Descricao,
                Lesao = a.Lesao,
                Consequencia = a.Consequencia,
                Atendimento = a.Atendimento,
                HouveAfastamento = a.HouveAfastamento,
                DiasAfastamento = a.DiasAfastamento,
                NumeroCat = a.NumeroCat,
                MetodologiaInvestigacao = a.MetodologiaInvestigacao,
                Causas = a.Causas,
                Status = a.Status,
                DataConclusaoInvestigacao = a.DataConclusaoInvestigacao,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"Acidente {request.Id} não encontrado.");

        var acoesPlano = await _db.AcoesPlano
            .Where(a => a.OrigemTipo == nameof(Domain.Entidades.Acidente) && a.OrigemId == request.Id)
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

        return new AcidenteDetalheDto
        {
            Acidente = acidente,
            AcoesPlano = acoesPlano,
        };
    }
}
