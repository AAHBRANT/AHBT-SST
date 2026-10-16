using AAHBRANT.SST.Application.LeituraIa;
using Xunit;

namespace AAHBRANT.SST.Application.Tests.LeituraIa;

public class LerCabecalhoDocumentoTests
{
    private sealed class ExtratorFalso(params string[] paginas) : IExtratorTextoPdf
    {
        public IReadOnlyList<string> ExtrairPaginas(byte[] pdf) => paginas;
    }

    private sealed class LeitorFalso : ILeitorCabecalhoDocumentoIa
    {
        public int PaginasRecebidas;
        public Task<CabecalhoCadastroPgrIa> LerCabecalhoPgrAsync(IReadOnlyList<string> paginas, CancellationToken ct)
        {
            PaginasRecebidas = paginas.Count;
            return Task.FromResult(new CabecalhoCadastroPgrIa("PGR - ROGER", "Parque Roger", null, null, "02", null, null, null));
        }

        public Task<CabecalhoCadastroPcmsoIa> LerCabecalhoPcmsoAsync(IReadOnlyList<string> paginas, CancellationToken ct)
            => Task.FromResult(new CabecalhoCadastroPcmsoIa("PCMSO", null, null, null, null, "Dr. X", "CRM/PB 1"));
    }

    [Fact]
    public async Task Le_so_as_primeiras_paginas()
    {
        var leitor = new LeitorFalso();
        var handler = new LerCabecalhoDocumentoHandler(new ExtratorFalso(Enumerable.Range(1, 40).Select(n => $"pág {n}").ToArray()), leitor);

        var r = await handler.Handle(new LerCabecalhoPgrQuery([1]), CancellationToken.None);

        Assert.Equal("PGR - ROGER", r.Nome);
        Assert.Equal(8, leitor.PaginasRecebidas);
    }

    [Fact]
    public async Task Pdf_sem_texto_pede_preenchimento_manual()
    {
        var handler = new LerCabecalhoDocumentoHandler(new ExtratorFalso("", "  "), new LeitorFalso());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new LerCabecalhoPcmsoQuery([1]), CancellationToken.None));
        Assert.Contains("manualmente", ex.Message);
    }
}
