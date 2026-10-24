using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.CursosTreinamento.Queries;

// Diagnóstico da trava de NR-06 no servidor (auditoria 06/10/2026): o servidor só reconhece como
// NR-06 o curso marcado AtendeNr6, e a marcação é derivada do texto da norma (só "NR-06", "NR-6",
// "NR06" ou "NR6"). Curso cuja norma CITA a NR-06 de outro jeito (ex.: "NR-06 e NR-18") não é
// marcado, e os certificados dele não liberam entrega de EPI. Esta lista mostra quais são, e
// quantos trabalhadores têm certificado válido neles, para o técnico corrigir a norma antes.
public record ListarCursosNr6SemMarcadorQuery : IRequest<List<CursoNr6SemMarcadorDto>>;

public record CursoNr6SemMarcadorDto(
    Guid Id,
    string Nome,
    string? NormaReferencia,
    int TrabalhadoresComCertificadoValido);

public class ListarCursosNr6SemMarcadorQueryHandler
    : IRequestHandler<ListarCursosNr6SemMarcadorQuery, List<CursoNr6SemMarcadorDto>>
{
    private readonly IAppDbContext _db;
    public ListarCursosNr6SemMarcadorQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<CursoNr6SemMarcadorDto>> Handle(ListarCursosNr6SemMarcadorQuery request, CancellationToken ct)
    {
        // Poucos cursos no catálogo: a heurística de texto roda em memória.
        var candidatos = (await _db.CursosTreinamento
                .Where(c => !c.AtendeNr6)
                .Select(c => new { c.Id, c.Nome, c.NormaReferencia })
                .ToListAsync(ct))
            .Where(c => CursoTreinamento.NormaMencionaNr6(c.NormaReferencia))
            .ToList();
        if (candidatos.Count == 0) return new List<CursoNr6SemMarcadorDto>();

        var ids = candidatos.Select(c => c.Id).ToList();
        var hoje = DateTime.UtcNow.AddHours(-3).Date; // dia em Brasília
        var validos = await _db.Treinamentos
            .Where(t => ids.Contains(t.CursoTreinamentoId) && t.DataValidade >= hoje)
            .Select(t => new { t.CursoTreinamentoId, t.TrabalhadorId })
            .Distinct()
            .ToListAsync(ct);

        return candidatos
            .Select(c => new CursoNr6SemMarcadorDto(
                c.Id, c.Nome, c.NormaReferencia, validos.Count(v => v.CursoTreinamentoId == c.Id)))
            .OrderBy(c => c.Nome)
            .ToList();
    }
}
