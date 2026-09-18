using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Terceirizados.Queries;

public record ListarPendenciasTerceirizadoQuery : IRequest<PainelPendenciasTerceirizadoDto>;

public class ListarPendenciasTerceirizadoQueryHandler : IRequestHandler<ListarPendenciasTerceirizadoQuery, PainelPendenciasTerceirizadoDto>
{
    private readonly IAppDbContext _db;
    public ListarPendenciasTerceirizadoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<PainelPendenciasTerceirizadoDto> Handle(ListarPendenciasTerceirizadoQuery request, CancellationToken ct)
    {
        var terceirizados = await _db.Trabalhadores
            .Where(t => t.Vinculo == TipoVinculo.Terceirizado)
            .Select(t => new { t.Id, t.Nome, t.FuncaoId, EmpresaId = t.EmpresaId!.Value, EmpresaRazaoSocial = t.Empresa!.RazaoSocial })
            .ToListAsync(ct);

        var pessoasBloqueadas = new List<PendenciaPessoaDto>();
        foreach (var t in terceirizados)
        {
            var pendencias = await CalculadoraLiberacaoTerceirizado.ObterPendenciasAsync(_db, t.Id, t.FuncaoId, ct);
            if (pendencias.Count > 0)
                pessoasBloqueadas.Add(new PendenciaPessoaDto(t.Id, t.Nome, t.EmpresaId, t.EmpresaRazaoSocial, pendencias));
        }

        var contratosEncerrados = await _db.Contratos
            .Where(c => c.Status == StatusContrato.Encerrado)
            .Select(c => new ContratoEncerradoComPessoasAtivasDto(
                c.Id, c.NumeroContrato, c.Empresa!.RazaoSocial,
                c.Trabalhadores.Count(t => t.DataDemissao == null)))
            .Where(c => c.QuantidadePessoasAtivas > 0)
            .ToListAsync(ct);

        var alertasEstoqueInsuficiente = await _db.Alertas
            .Where(a => a.Tipo == TipoAlerta.EpiEstoqueInsuficiente && a.Status == StatusAlerta.Aberto)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new AlertaEstoqueInsuficienteDto(a.Id, a.Titulo, a.Descricao, a.CreatedAtUtc))
            .ToListAsync(ct);

        return new PainelPendenciasTerceirizadoDto(pessoasBloqueadas, contratosEncerrados, alertasEstoqueInsuficiente);
    }
}
