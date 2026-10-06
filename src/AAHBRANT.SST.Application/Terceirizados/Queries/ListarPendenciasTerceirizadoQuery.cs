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
            .Where(t => t.Vinculo == TipoVinculo.Terceirizado && t.EmpresaId != null)
            .Select(t => new { t.Id, t.Nome, t.FuncaoId, EmpresaId = t.EmpresaId!.Value, EmpresaRazaoSocial = t.Empresa!.RazaoSocial })
            .ToListAsync(ct);

        var pessoasBloqueadas = new List<PendenciaPessoaDto>();
        foreach (var t in terceirizados)
        {
            var pendencias = await CalculadoraLiberacaoTerceirizado.ObterPendenciasAsync(_db, t.Id, t.FuncaoId, ct);
            if (pendencias.Count > 0)
                pessoasBloqueadas.Add(new PendenciaPessoaDto(t.Id, t.Nome, t.EmpresaId, t.EmpresaRazaoSocial, pendencias));
        }

        // O filtro "tem pessoa ativa" não pode vir depois do Select para o DTO: o EF Core não traduz
        // um Where sobre a propriedade de um construtor que embute a subconsulta Count (erro 400 no
        // SQL real, invisível no InMemory). Projeta para tipo anônimo e filtra na memória.
        var contratosEncerradosBrutos = await _db.Contratos
            .Where(c => c.Status == StatusContrato.Encerrado)
            .Select(c => new
            {
                c.Id,
                c.NumeroContrato,
                EmpresaRazaoSocial = c.Empresa!.RazaoSocial,
                QuantidadePessoasAtivas = c.Trabalhadores.Count(t => t.DataDemissao == null),
            })
            .ToListAsync(ct);
        var contratosEncerrados = contratosEncerradosBrutos
            .Where(c => c.QuantidadePessoasAtivas > 0)
            .Select(c => new ContratoEncerradoComPessoasAtivasDto(c.Id, c.NumeroContrato, c.EmpresaRazaoSocial, c.QuantidadePessoasAtivas))
            .ToList();

        var alertasEstoqueInsuficiente = await _db.Alertas
            .Where(a => a.Tipo == TipoAlerta.EpiEstoqueInsuficiente && a.Status == StatusAlerta.Aberto)
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new AlertaEstoqueInsuficienteDto(a.Id, a.Titulo, a.Descricao, a.CreatedAtUtc))
            .ToListAsync(ct);

        return new PainelPendenciasTerceirizadoDto(pessoasBloqueadas, contratosEncerrados, alertasEstoqueInsuficiente);
    }
}
