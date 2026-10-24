using AAHBRANT.SST.Application.Alojamentos.Queries;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Veiculos.Queries;

public record ListarVeiculosQuery(Guid? ObraId, TipoVeiculo? Tipo) : IRequest<List<VeiculoResumoDto>>;

// O resumo de cada inspeção reaproveita AlojamentoInspecaoResumoDto (mesmos campos: data, status,
// itens respondidos/NC e documento de assinatura) — não vale duplicar o record.
// StatusUltimaInspecao: "nunca" (sem inspeção concluída) ou "inspecionado" (há uma concluída).
// Inspeção avulsa, sem prazo de atraso (decisão do usuário em 02/10/2026).
public record VeiculoResumoDto(
    Guid Id,
    Guid ObraId,
    TipoVeiculo Tipo,
    string PlacaPrefixo,
    string? MarcaModelo,
    int? Ano,
    string? Cor,
    string? Empresa,
    string? Responsavel,
    string StatusUltimaInspecao,
    int? DiasDesdeUltimaInspecao,
    AlojamentoInspecaoResumoDto? InspecaoEmAndamento,
    AlojamentoInspecaoResumoDto? UltimaInspecaoConcluida,
    List<AlojamentoInspecaoResumoDto> HistoricoInspecoes);

public class ListarVeiculosQueryHandler : IRequestHandler<ListarVeiculosQuery, List<VeiculoResumoDto>>
{
    private readonly IAppDbContext _db;
    public ListarVeiculosQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<VeiculoResumoDto>> Handle(ListarVeiculosQuery request, CancellationToken ct)
    {
        var hoje = DateTime.UtcNow;

        var query = _db.Veiculos.AsNoTracking();
        if (request.ObraId is { } obraId) query = query.Where(v => v.ObraId == obraId);
        if (request.Tipo is { } tipo) query = query.Where(v => v.Tipo == tipo);

        var veiculos = await query
            .OrderBy(v => v.PlacaPrefixo)
            .Select(v => new
            {
                v.Id,
                v.ObraId,
                v.Tipo,
                v.PlacaPrefixo,
                v.MarcaModelo,
                v.Ano,
                v.Cor,
                v.Empresa,
                v.Responsavel,
                Inspecoes = _db.Inspecoes
                    .Where(i => i.VeiculoId == v.Id)
                    .OrderByDescending(i => i.Data)
                    .Select(i => new AlojamentoInspecaoResumoDto(
                        i.Id,
                        i.Data,
                        i.Status,
                        i.Respostas.Count(r => r.Ativo),
                        i.Respostas.Count(r => r.Ativo && r.StatusItem != null),
                        i.Respostas.Count(r => r.Ativo && r.StatusItem == StatusItemChecklist.NaoConforme),
                        _db.DocumentosAssinatura
                            .Where(d => d.EntidadeTipo == nameof(AAHBRANT.SST.Domain.Entidades.Inspecao) && d.EntidadeId == i.Id)
                            .Select(d => new AlojamentoDocumentoAssinaturaResumoDto(
                                d.Id,
                                d.Status,
                                d.PdfConteudo != null,
                                d.FinalizadoEm))
                            .FirstOrDefault()))
                    .ToList(),
            })
            .ToListAsync(ct);

        return veiculos.Select(v =>
        {
            var emAndamento = v.Inspecoes.FirstOrDefault(i => i.Status == StatusInspecao.EmAndamento);
            var ultimaConcluida = v.Inspecoes.FirstOrDefault(i => i.Status == StatusInspecao.Concluida);
            return new VeiculoResumoDto(
                v.Id, v.ObraId, v.Tipo, v.PlacaPrefixo, v.MarcaModelo, v.Ano, v.Cor, v.Empresa, v.Responsavel,
                ultimaConcluida is null ? "nunca" : "inspecionado",
                ultimaConcluida is null ? null : (int)(hoje - ultimaConcluida.Data).TotalDays,
                emAndamento, ultimaConcluida, v.Inspecoes);
        }).ToList();
    }
}
