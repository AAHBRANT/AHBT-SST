using AAHBRANT.SST.Application.Cipa.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Queries;

// Dados que o tablet da urna precisa. Nunca devolve contagem de votos por candidato enquanto a
// votação está aberta — só o comparecimento ("X de Y votaram").
public record ObterUrnaCipaQuery(Guid ProcessoEleitoralId) : IRequest<UrnaCipaDto?>;

public record CandidatoUrnaCipaDto(Guid CandidatoId, Guid TrabalhadorId, string Nome, string Funcao, bool TemFoto);

public record UrnaCipaDto(
    Guid ProcessoEleitoralId,
    Guid ObraId,
    string ObraNome,
    string? NumeroDocumento,
    StatusProcessoEleitoralCipa Status,
    DateTime? VotacaoAbertaEmUtc,
    DateTime? VotacaoEncerradaEmUtc,
    int TotalEleitoresAptos,
    int TotalVotantes,
    List<CandidatoUrnaCipaDto> Candidatos);

public class ObterUrnaCipaQueryHandler : IRequestHandler<ObterUrnaCipaQuery, UrnaCipaDto?>
{
    private readonly IAppDbContext _db;
    public ObterUrnaCipaQueryHandler(IAppDbContext db) => _db = db;

    public async Task<UrnaCipaDto?> Handle(ObterUrnaCipaQuery request, CancellationToken ct)
    {
        var processo = await _db.ProcessosEleitoraisCipa
            .Where(p => p.Id == request.ProcessoEleitoralId)
            .Select(p => new { p.Id, p.ObraId, ObraNome = p.Obra!.Nome, p.NumeroDocumento, p.Status, p.VotacaoAbertaEmUtc, p.VotacaoEncerradaEmUtc, p.TotalEleitoresAptos })
            .FirstOrDefaultAsync(ct);
        if (processo is null) return null;

        var candidatos = await _db.CandidatosCipa
            .Where(c => c.ProcessoEleitoralId == processo.Id && c.Status == StatusCandidatoCipa.Deferido)
            .OrderBy(c => c.Trabalhador!.Nome)
            .Select(c => new CandidatoUrnaCipaDto(c.Id, c.TrabalhadorId, c.Trabalhador!.Nome, c.Trabalhador.Funcao!.Nome, c.Trabalhador.FotoConteudo != null))
            .ToListAsync(ct);

        var votantes = await _db.EleitoresCipa.CountAsync(e => e.ProcessoEleitoralId == processo.Id && e.VotouEmUtc != null, ct);
        var aptos = processo.TotalEleitoresAptos ?? await UrnaCipa.EleitoresAptos(_db, processo.ObraId).CountAsync(ct);

        return new UrnaCipaDto(processo.Id, processo.ObraId, processo.ObraNome, processo.NumeroDocumento, processo.Status,
            processo.VotacaoAbertaEmUtc, processo.VotacaoEncerradaEmUtc, aptos, votantes, candidatos);
    }
}

// Foto do candidato para a urna — só de candidato deferido desta eleição (não expõe foto de qualquer
// trabalhador para quem tem apenas permissão da CIPA).
public record ObterFotoCandidatoUrnaCipaQuery(Guid ProcessoEleitoralId, Guid CandidatoId) : IRequest<(byte[] Conteudo, string ContentType)?>;

public class ObterFotoCandidatoUrnaCipaQueryHandler : IRequestHandler<ObterFotoCandidatoUrnaCipaQuery, (byte[] Conteudo, string ContentType)?>
{
    private readonly IAppDbContext _db;
    public ObterFotoCandidatoUrnaCipaQueryHandler(IAppDbContext db) => _db = db;

    public async Task<(byte[] Conteudo, string ContentType)?> Handle(ObterFotoCandidatoUrnaCipaQuery request, CancellationToken ct)
    {
        var foto = await _db.CandidatosCipa
            .Where(c => c.Id == request.CandidatoId && c.ProcessoEleitoralId == request.ProcessoEleitoralId && c.Status == StatusCandidatoCipa.Deferido)
            .Select(c => new { c.Trabalhador!.FotoConteudo, c.Trabalhador.FotoContentType })
            .FirstOrDefaultAsync(ct);
        if (foto?.FotoConteudo is null) return null;
        return (foto.FotoConteudo, foto.FotoContentType ?? "image/jpeg");
    }
}
