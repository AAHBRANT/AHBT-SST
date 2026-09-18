using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Contratos.Queries;

public record ObterContratoDetalheQuery(Guid Id) : IRequest<ContratoDetalheDto?>;

public class ObterContratoDetalheQueryHandler : IRequestHandler<ObterContratoDetalheQuery, ContratoDetalheDto?>
{
    private readonly IAppDbContext _db;
    public ObterContratoDetalheQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ContratoDetalheDto?> Handle(ObterContratoDetalheQuery request, CancellationToken ct)
    {
        var contrato = await _db.Contratos
            .Where(c => c.Id == request.Id)
            .Select(c => new
            {
                c.Id, c.EmpresaId, EmpresaRazaoSocial = c.Empresa!.RazaoSocial,
                c.ObraId, ObraNome = c.Obra!.Nome, c.NumeroContrato,
                c.DataInicioVigencia, c.DataFimVigencia, c.Status,
            })
            .FirstOrDefaultAsync(ct);
        if (contrato is null) return null;

        var vagas = await _db.ContratoVagasFuncao
            .Where(v => v.ContratoId == request.Id)
            .OrderBy(v => v.Funcao!.Nome)
            .Select(v => new VagaFuncaoDto(v.Id, v.FuncaoId, v.Funcao!.Nome, v.QuantidadeVagas, v.QuantidadePreenchidas))
            .ToListAsync(ct);

        return new ContratoDetalheDto(
            contrato.Id, contrato.EmpresaId, contrato.EmpresaRazaoSocial, contrato.ObraId, contrato.ObraNome,
            contrato.NumeroContrato, contrato.DataInicioVigencia, contrato.DataFimVigencia,
            contrato.Status.ToString(), vagas);
    }
}
