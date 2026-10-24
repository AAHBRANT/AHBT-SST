using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Documentos;

public class GeradorNumeroDocumentoService : IGeradorNumeroDocumentoService
{
    private readonly IAppDbContext _db;

    public GeradorNumeroDocumentoService(IAppDbContext db) => _db = db;

    public async Task<string> GerarAsync(string prefixo, CancellationToken ct)
    {
        var ano = DateTime.UtcNow.Year;

        // Local primeiro: várias chamadas antes do SaveChanges (ex.: um número por participante no
        // encerramento da turma) precisam enxergar o contador recém-criado e ainda não salvo.
        var contador = _db.ContadoresDocumento.Local.FirstOrDefault(c => c.Prefixo == prefixo && c.Ano == ano)
            ?? await _db.ContadoresDocumento.FirstOrDefaultAsync(c => c.Prefixo == prefixo && c.Ano == ano, ct);
        if (contador is null)
        {
            contador = new ContadorDocumento { Prefixo = prefixo, Ano = ano, UltimoNumero = 0 };
            _db.ContadoresDocumento.Add(contador);
        }
        contador.UltimoNumero++;

        return $"{prefixo}-{ano}-{contador.UltimoNumero:D4}";
    }
}
