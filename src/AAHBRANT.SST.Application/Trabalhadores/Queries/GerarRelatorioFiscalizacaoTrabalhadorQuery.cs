using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Queries;

// Relatório único de fiscalização (MTE) — reaproveita a mesma agregação de
// ObterPerfilCompletoTrabalhadorQuery injetando o handler diretamente (não IMediator, que não é usado
// dentro de handlers da Application neste projeto — ver nota de arquitetura do módulo), para não
// duplicar as 6 seções do perfil num segundo lugar.
public record GerarRelatorioFiscalizacaoTrabalhadorQuery(Guid Id) : IRequest<byte[]?>;

public class GerarRelatorioFiscalizacaoTrabalhadorQueryHandler : IRequestHandler<GerarRelatorioFiscalizacaoTrabalhadorQuery, byte[]?>
{
    private readonly IRequestHandler<ObterPerfilCompletoTrabalhadorQuery, PerfilCompletoTrabalhadorDto?> _obterPerfil;
    private readonly IRelatorioFiscalizacaoPdfService _pdf;
    private readonly IAppDbContext? _db;

    public GerarRelatorioFiscalizacaoTrabalhadorQueryHandler(
        IRequestHandler<ObterPerfilCompletoTrabalhadorQuery, PerfilCompletoTrabalhadorDto?> obterPerfil,
        IRelatorioFiscalizacaoPdfService pdf,
        IAppDbContext? db = null)
    {
        _obterPerfil = obterPerfil;
        _pdf = pdf;
        _db = db;
    }

    public async Task<byte[]?> Handle(GerarRelatorioFiscalizacaoTrabalhadorQuery request, CancellationToken ct)
    {
        var perfil = await _obterPerfil.Handle(new ObterPerfilCompletoTrabalhadorQuery(request.Id), ct);
        if (perfil is null) return null;

        // Foto de cadastro e dados da obra para a grade de identificação. Ficam fora do DTO do perfil
        // para não irem em bytes na resposta JSON da API.
        IdentificacaoRelatorioFiscalizacao? identificacao = null;
        if (_db is not null)
        {
            var dados = await _db.Trabalhadores.AsNoTracking()
                .Where(t => t.Id == request.Id)
                .Select(t => new { t.FotoConteudo, Cliente = t.Obra!.Cliente, Cnpj = t.Obra!.Cnpj })
                .FirstOrDefaultAsync(ct);
            if (dados is not null)
                identificacao = new IdentificacaoRelatorioFiscalizacao(dados.FotoConteudo, dados.Cliente, dados.Cnpj);
        }

        return _pdf.Gerar(perfil, identificacao);
    }
}
