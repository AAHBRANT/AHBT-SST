using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
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
    private readonly IImagemBiometricaCriptografia? _imagemCriptografia;

    public GerarRelatorioFiscalizacaoTrabalhadorQueryHandler(
        IRequestHandler<ObterPerfilCompletoTrabalhadorQuery, PerfilCompletoTrabalhadorDto?> obterPerfil,
        IRelatorioFiscalizacaoPdfService pdf,
        IAppDbContext? db = null,
        IImagemBiometricaCriptografia? imagemCriptografia = null)
    {
        _obterPerfil = obterPerfil;
        _pdf = pdf;
        _db = db;
        _imagemCriptografia = imagemCriptografia;
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
                identificacao = new IdentificacaoRelatorioFiscalizacao(dados.FotoConteudo, dados.Cliente, dados.Cnpj,
                    await CarregarReferenciasAsync(request.Id, perfil.Assinaturas, ct));
        }

        return _pdf.Gerar(perfil, identificacao);
    }

    // Mesma regra da Ficha de EPI (ExportarFichaEpiTrabalhadorQuery): vale o cadastro vigente na data de
    // cada assinatura, incluindo os arquivados — facial → foto facial cadastrada; digital → imagem da
    // digital cadastrada (descriptografada).
    private async Task<IReadOnlyDictionary<Guid, byte[]>> CarregarReferenciasAsync(
        Guid trabalhadorId, IEnumerable<AssinaturaPerfilDto> assinaturas, CancellationToken ct)
    {
        var resultado = new Dictionary<Guid, byte[]>();
        var lista = assinaturas.ToList();
        if (_db is null || lista.Count == 0) return resultado;

        var fotosFace = lista.Any(a => a.Metodo == MetodoAutenticacaoAssinatura.ReconhecimentoFacial)
            ? await _db.FotosCadastroFacial.AsNoTracking().IgnoreQueryFilters()
                .Where(f => f.TrabalhadorId == trabalhadorId).OrderBy(f => f.CapturadaEm)
                .Select(f => new { f.Conteudo, f.CapturadaEm }).ToListAsync(ct)
            : new();
        var templates = lista.Any(a => a.Metodo == MetodoAutenticacaoAssinatura.Biometria)
            ? await _db.TemplatesBiometricoFutronic.AsNoTracking().IgnoreQueryFilters()
                .Where(t => t.TrabalhadorId == trabalhadorId).OrderBy(t => t.CapturadoEm)
                .Select(t => new { t.CapturadoEm, t.ImagemCadastroCriptografada }).ToListAsync(ct)
            : new();

        foreach (var a in lista)
        {
            byte[]? referencia = null;
            if (a.Metodo == MetodoAutenticacaoAssinatura.ReconhecimentoFacial)
                referencia = fotosFace.LastOrDefault(f => f.CapturadaEm <= a.AssinadoEm)?.Conteudo;
            else if (a.Metodo == MetodoAutenticacaoAssinatura.Biometria && _imagemCriptografia is not null)
            {
                var cifrada = templates.LastOrDefault(t => t.CapturadoEm <= a.AssinadoEm)?.ImagemCadastroCriptografada;
                if (!string.IsNullOrEmpty(cifrada))
                {
                    try { referencia = _imagemCriptografia.Descriptografar(cifrada); }
                    catch { referencia = null; }
                }
            }
            if (referencia is { Length: > 0 }) resultado[a.SignatarioId] = referencia;
        }
        return resultado;
    }
}
