using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Pcmso.Queries;

// Monta os dois componentes do PCMSO que não viram tabela própria (ver disclosure em
// Domain/Entidades/Pcmso/Pcmso.cs): o calendário de exames e o relatório analítico de saúde são
// CALCULADOS aqui a cada consulta, a partir da matriz + dos trabalhadores ativos da obra + dos
// ASOs já registrados — sem duplicar dado em tabela nova.
public record ObterPcmsoDetalheQuery(Guid Id) : IRequest<PcmsoDetalheDto?>;

public class ObterPcmsoDetalheQueryHandler : IRequestHandler<ObterPcmsoDetalheQuery, PcmsoDetalheDto?>
{
    private readonly IAppDbContext _db;

    public ObterPcmsoDetalheQueryHandler(IAppDbContext db) => _db = db;

    public async Task<PcmsoDetalheDto?> Handle(ObterPcmsoDetalheQuery request, CancellationToken ct)
    {
        var pcmso = await _db.Pcmsos.FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        if (pcmso is null)
        {
            return null;
        }

        var itensMatriz = await _db.PcmsoItensMatriz
            .Where(i => i.PcmsoId == pcmso.Id)
            .Select(i => new
            {
                i.Id,
                i.PcmsoId,
                i.FuncaoId,
                FuncaoNome = i.Funcao!.Nome,
                i.RiscoId,
                i.NomeExame,
                i.PeriodicidadeEmMeses,
                i.ObrigatorioNoAdmissional,
                i.ObrigatorioNoDemissional,
                i.Observacoes,
            })
            .ToListAsync(ct);

        var revisoes = await _db.PcmsoRevisoes
            .Where(r => r.PcmsoId == pcmso.Id)
            .OrderByDescending(r => r.NumeroRevisao)
            .ToListAsync(ct);

        // Calendário + relatório: só fazem sentido se a matriz já tiver ao menos um item (senão
        // não há "próximo exame previsto" para calcular).
        var funcaoIds = itensMatriz.Select(i => i.FuncaoId).Distinct().ToList();
        var calendario = new List<ItemCalendarioExameDto>();
        var relatorio = new List<LinhaRelatorioAnaliticoDto>();

        if (funcaoIds.Count > 0)
        {
            var trabalhadores = await _db.Trabalhadores
                .Where(t => t.ObraId == pcmso.ObraId && funcaoIds.Contains(t.FuncaoId) && t.DataDemissao == null)
                .Select(t => new { t.Id, t.Nome, t.FuncaoId, t.DataAdmissao })
                .ToListAsync(ct);

            var trabalhadorIds = trabalhadores.Select(t => t.Id).ToList();

            var ultimosExamesPorTrabalhador = (await _db.Asos
                .Where(a => trabalhadorIds.Contains(a.TrabalhadorId))
                .GroupBy(a => a.TrabalhadorId)
                .Select(g => new { TrabalhadorId = g.Key, UltimaData = g.Max(a => a.DataExame) })
                .ToListAsync(ct))
                .ToDictionary(x => x.TrabalhadorId, x => x.UltimaData);

            var hoje = DateTime.UtcNow.Date;
            foreach (var item in itensMatriz)
            {
                foreach (var trabalhador in trabalhadores.Where(t => t.FuncaoId == item.FuncaoId))
                {
                    var ultimoExame = ultimosExamesPorTrabalhador.TryGetValue(trabalhador.Id, out var data)
                        ? (DateTime?)data
                        : null;
                    var dataBase = ultimoExame ?? trabalhador.DataAdmissao;
                    var proximaData = dataBase.AddMonths(item.PeriodicidadeEmMeses);

                    calendario.Add(new ItemCalendarioExameDto
                    {
                        TrabalhadorId = trabalhador.Id,
                        TrabalhadorNome = trabalhador.Nome,
                        FuncaoId = item.FuncaoId,
                        FuncaoNome = item.FuncaoNome,
                        NomeExame = item.NomeExame,
                        UltimoExameData = ultimoExame,
                        ProximaDataPrevista = proximaData,
                        Vencido = proximaData.Date < hoje,
                    });
                }
            }
            calendario = calendario.OrderBy(c => c.ProximaDataPrevista).ToList();

            var asosPorFuncao = await _db.Asos
                .Where(a => trabalhadorIds.Contains(a.TrabalhadorId))
                .Select(a => new { a.ResultadoStatus, FuncaoId = a.Trabalhador!.FuncaoId })
                .ToListAsync(ct);

            relatorio = asosPorFuncao
                .GroupBy(a => a.FuncaoId)
                .Select(g => new LinhaRelatorioAnaliticoDto
                {
                    FuncaoId = g.Key,
                    FuncaoNome = itensMatriz.FirstOrDefault(i => i.FuncaoId == g.Key)?.FuncaoNome ?? string.Empty,
                    TotalAsos = g.Count(),
                    Aptos = g.Count(a => a.ResultadoStatus == ResultadoAso.Apto),
                    AptosComRestricao = g.Count(a => a.ResultadoStatus == ResultadoAso.AptoComRestricao),
                    Inaptos = g.Count(a => a.ResultadoStatus == ResultadoAso.Inapto),
                    Pendentes = g.Count(a => a.ResultadoStatus == ResultadoAso.Pendente),
                })
                .OrderBy(l => l.FuncaoNome)
                .ToList();
        }

        return new PcmsoDetalheDto
        {
            Pcmso = new PcmsoDto
            {
                Id = pcmso.Id,
                ObraId = pcmso.ObraId,
                Nome = pcmso.Nome,
                Objetivo = pcmso.Objetivo,
                MedicoCoordenadorNome = pcmso.MedicoCoordenadorNome,
                MedicoCoordenadorCrm = pcmso.MedicoCoordenadorCrm,
                MedicoCoordenadorUsuarioId = pcmso.MedicoCoordenadorUsuarioId,
                DataElaboracao = pcmso.DataElaboracao,
                DataVigenciaInicio = pcmso.DataVigenciaInicio,
                DataVigenciaFim = pcmso.DataVigenciaFim,
                Status = pcmso.Status,
            },
            ItensMatriz = itensMatriz.Select(i => new PcmsoItemMatrizDto
            {
                Id = i.Id,
                PcmsoId = i.PcmsoId,
                FuncaoId = i.FuncaoId,
                FuncaoNome = i.FuncaoNome,
                RiscoId = i.RiscoId,
                NomeExame = i.NomeExame,
                PeriodicidadeEmMeses = i.PeriodicidadeEmMeses,
                ObrigatorioNoAdmissional = i.ObrigatorioNoAdmissional,
                ObrigatorioNoDemissional = i.ObrigatorioNoDemissional,
                Observacoes = i.Observacoes,
            }).ToList(),
            Revisoes = revisoes.Select(r => new PcmsoRevisaoDto
            {
                Id = r.Id,
                PcmsoId = r.PcmsoId,
                NumeroRevisao = r.NumeroRevisao,
                DataRevisao = r.DataRevisao,
                Motivo = r.Motivo,
                ResponsavelUsuarioId = r.ResponsavelUsuarioId,
            }).ToList(),
            Calendario = calendario,
            RelatorioAnalitico = relatorio,
        };
    }
}
