using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Terceirizados.Queries;

public record ListarPessoasTerceirizadasQuery(Guid? EmpresaId, Guid? ContratoId) : IRequest<List<PessoaTerceirizadaDto>>;

public class ListarPessoasTerceirizadasQueryHandler : IRequestHandler<ListarPessoasTerceirizadasQuery, List<PessoaTerceirizadaDto>>
{
    private readonly IAppDbContext _db;
    public ListarPessoasTerceirizadasQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<PessoaTerceirizadaDto>> Handle(ListarPessoasTerceirizadasQuery request, CancellationToken ct)
    {
        var query = _db.Trabalhadores.Where(t => t.Vinculo == TipoVinculo.Terceirizado);
        if (request.EmpresaId.HasValue) query = query.Where(t => t.EmpresaId == request.EmpresaId);
        if (request.ContratoId.HasValue) query = query.Where(t => t.ContratoId == request.ContratoId);

        var trabalhadores = await query
            .OrderBy(t => t.Nome)
            .Select(t => new
            {
                t.Id, t.Nome, t.Matricula,
                EmpresaId = t.EmpresaId!.Value, EmpresaRazaoSocial = t.Empresa!.RazaoSocial,
                ContratoId = t.ContratoId!.Value, NumeroContrato = t.Contrato!.NumeroContrato,
                t.FuncaoId, FuncaoNome = t.Funcao!.Nome,
            })
            .ToListAsync(ct);

        var resultado = new List<PessoaTerceirizadaDto>();
        foreach (var t in trabalhadores)
        {
            var pendencias = await CalculadoraLiberacaoTerceirizado.ObterPendenciasAsync(_db, t.Id, t.FuncaoId, ct);
            var status = pendencias.Count == 0 ? StatusLiberacaoTerceirizado.Liberada : StatusLiberacaoTerceirizado.Pendente;
            resultado.Add(new PessoaTerceirizadaDto(
                t.Id, t.Nome, t.Matricula, t.EmpresaId, t.EmpresaRazaoSocial, t.ContratoId, t.NumeroContrato,
                t.FuncaoId, t.FuncaoNome, status, pendencias));
        }
        return resultado;
    }
}
