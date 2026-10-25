using AAHBRANT.SST.Application.MatrizRisco;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.GeracaoPgr;

// PDF do PGR gerado pela EAP (próxima etapa após a aprovação, 23/10/2026). Só sai depois da aprovação do
// Engenheiro de Segurança: antes disso o conteúdo ainda é sugestão. Gera a partir da estrutura aprovada
// (GeracaoPgrEap.EstruturaJson), sem guardar um segundo arquivo.
public interface IPgrPdfService
{
    byte[] Gerar(DadosPgrPdf dados, byte[]? logoObra);
}

public record CelulaMatrizPdf(int Probabilidade, int Severidade, NivelRisco Nivel);

// Matriz de risco da organização, usada na seção "Matriz de risco" e para dar o nível de cada risco.
public record MatrizRiscoPdf(int MaxProbabilidade, int MaxSeveridade, IReadOnlyList<CelulaMatrizPdf> Celulas);

public record DadosPgrPdf(
    string ObraNome,
    string? ObraCnpj,
    string Nome,
    DateTime DataElaboracao,
    DateTime? DataProximaRevisao,
    string? ElaboradoPorNome,
    string NomeArquivoEap,
    string? AprovadoPorNome,
    DateTime? AprovadoEmUtc,
    EstruturaGeracaoPgr Estrutura,
    DateTime GeradoEmUtc,
    string? ObraEndereco = null,
    MatrizRiscoPdf? Matriz = null,
    string? ObraCidade = null,
    string? ObraUf = null,
    IReadOnlyList<LinhaFuncaoPdf>? Funcoes = null,
    IReadOnlyList<RevisaoPdf>? Revisoes = null,
    int? NumeroFuncionarios = null,
    IReadOnlyDictionary<Guid, CatalogoEpiInfo>? Epis = null);

// Função de um GHE com o CBO do cadastro e a quantidade de expostos (trabalhadores ativos da obra na função).
public record LinhaFuncaoPdf(int GheNumero, string Nome, string? Cbo, int Expostos);

public record RevisaoPdf(int Numero, DateTime Data, string Motivo, string? Responsavel);

public record ArquivoPgrPdf(byte[] Conteudo, string NomeArquivo);

public record GerarPdfGeracaoPgrEapQuery(Guid Id) : IRequest<ArquivoPgrPdf>;

public class GerarPdfGeracaoPgrEapQueryHandler : IRequestHandler<GerarPdfGeracaoPgrEapQuery, ArquivoPgrPdf>
{
    private readonly IAppDbContext _db;
    private readonly IPgrPdfService _pdf;

    public GerarPdfGeracaoPgrEapQueryHandler(IAppDbContext db, IPgrPdfService pdf)
    {
        _db = db;
        _pdf = pdf;
    }

    public async Task<ArquivoPgrPdf> Handle(GerarPdfGeracaoPgrEapQuery request, CancellationToken ct)
    {
        var g = await SerializacaoGeracao.CarregarAsync(_db, request.Id, ct);
        if (g.Status != StatusGeracaoPgrEap.Aprovado)
            throw new InvalidOperationException("O PDF do PGR só é gerado depois da aprovação do Engenheiro de Segurança.");

        var obra = await _db.Obras.Where(o => o.Id == g.ObraId)
            .Select(o => new { o.Nome, o.Cnpj, o.Endereco, o.Cidade, o.Uf, o.LogoConteudo })
            .FirstOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Obra não encontrada.");
        var estrutura = SerializacaoGeracao.Ler(g.EstruturaJson);

        // CBO do cadastro (por id, ou pelo nome quando a função ainda não tinha id) e expostos = trabalhadores
        // ativos da obra naquela função.
        var funcoesCadastro = await _db.Funcoes.Select(f => new { f.Id, f.Nome, f.CboCodigo }).ToListAsync(ct);
        var porNome = new Dictionary<string, (Guid Id, string? Cbo)>();
        foreach (var f in funcoesCadastro) porNome.TryAdd(FuncaoSstClassifier.Normalizar(f.Nome), (f.Id, f.CboCodigo));
        var expostosPorFuncao = (await _db.Trabalhadores.Where(t => t.ObraId == g.ObraId && t.DataDemissao == null)
                .Select(t => t.FuncaoId).ToListAsync(ct))
            .GroupBy(id => id).ToDictionary(x => x.Key, x => x.Count());
        var linhasFuncao = new List<LinhaFuncaoPdf>();
        foreach (var ghe in estrutura.Ghes.OrderBy(x => x.Numero))
            foreach (var f in ghe.Funcoes)
            {
                Guid? id = f.FuncaoId;
                string? cbo = id is { } fid ? funcoesCadastro.FirstOrDefault(c => c.Id == fid)?.CboCodigo : null;
                if (id is null && porNome.TryGetValue(FuncaoSstClassifier.Normalizar(f.Nome), out var achada))
                    (id, cbo) = (achada.Id, achada.Cbo);
                linhasFuncao.Add(new LinhaFuncaoPdf(ghe.Numero, f.Nome, string.IsNullOrWhiteSpace(cbo) ? null : cbo.Trim(),
                    id is { } i ? expostosPorFuncao.GetValueOrDefault(i) : 0));
            }

        // Controle de revisões: as registradas no PGR aprovado; sem nenhuma, a emissão inicial (revisão 00).
        var revisoesDb = g.PgrId is { } pgrId
            ? await _db.PgrRevisoes.Where(r => r.PgrId == pgrId).OrderBy(r => r.NumeroRevisao)
                .Select(r => new { r.NumeroRevisao, r.DataRevisao, r.Motivo, r.ResponsavelUsuarioId }).ToListAsync(ct)
            : null;
        var idsResp = (revisoesDb ?? new()).Where(r => r.ResponsavelUsuarioId != null).Select(r => r.ResponsavelUsuarioId!.Value).Distinct().ToList();
        var nomesResp = idsResp.Count == 0 ? new Dictionary<Guid, string>()
            : await _db.Usuarios.Where(u => idsResp.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Nome, ct);
        var revisoes = revisoesDb is { Count: > 0 }
            ? revisoesDb.Select(r => new RevisaoPdf(r.NumeroRevisao, r.DataRevisao, r.Motivo,
                r.ResponsavelUsuarioId is { } u ? nomesResp.GetValueOrDefault(u) : null)).ToList()
            : new List<RevisaoPdf> { new(0, g.DataElaboracao, "Emissão inicial.", g.ElaboradoPorNome) };

        var config = await _db.MatrizRiscoConfigs.Include(m => m.Celulas).VigenteMaisRecente().FirstOrDefaultAsync(ct);
        var matriz = config is null
            ? null
            : new MatrizRiscoPdf(
                config.NumNiveisProbabilidade,
                config.NumNiveisSeveridade,
                config.Celulas.Select(c => new CelulaMatrizPdf(c.Probabilidade, c.Severidade, c.NivelRisco)).ToList());

        var dados = new DadosPgrPdf(
            obra.Nome,
            obra.Cnpj,
            g.Nome,
            g.DataElaboracao,
            g.DataProximaRevisao,
            g.ElaboradoPorNome,
            g.NomeArquivoEap,
            g.AprovadoPorNome,
            g.AprovadoEmUtc,
            estrutura,
            DateTime.UtcNow,
            obra.Endereco,
            matriz,
            obra.Cidade,
            obra.Uf,
            linhasFuncao,
            revisoes,
            expostosPorFuncao.Values.Sum(),
            await SerializacaoGeracao.CarregarEpisAsync(_db, estrutura, ct));

        var nomeArquivo = $"PGR - {obra.Nome} - {g.DataElaboracao:yyyyMMdd}.pdf";
        return new ArquivoPgrPdf(_pdf.Gerar(dados, obra.LogoConteudo), nomeArquivo);
    }
}
