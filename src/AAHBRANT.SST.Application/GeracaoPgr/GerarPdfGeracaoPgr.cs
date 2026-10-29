using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Cbo;
using AAHBRANT.SST.Application.MatrizRisco;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
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
    IReadOnlyDictionary<Guid, CatalogoEpiInfo>? Epis = null,
    IReadOnlyList<LinhaTreinamentoPdf>? Treinamentos = null,
    // Assinaturas do PGR (técnico e engenheiro). Nulo em rascunho antigo, que mantém as duas linhas em branco.
    IReadOnlyList<AssinaturaPgrPdf>? Assinaturas = null,
    // Código, endereço e QR de verificação: só existem depois de o documento do motor ser finalizado.
    VerificacaoPgrPdf? Verificacao = null,
    // Minuta para o engenheiro ler antes de assinar: marcada como "não assinado" em todas as páginas.
    bool Minuta = false,
    // Prévia do técnico antes de enviar para aprovação (29/10/2026): a minuta sai carimbada como RASCUNHO.
    bool Rascunho = false);

// Uma linha de assinatura. AssinadoEmUtc preenchido = carimbo de assinatura eletrônica; AMao = a linha sai em branco, para
// assinatura fora do sistema; sem as duas coisas, ainda não assinou.
public record AssinaturaPgrPdf(string Funcao, string? Nome, string? Registro, string Descricao, DateTime? AssinadoEmUtc, bool AMao);

public record VerificacaoPgrPdf(string Codigo, string Url, byte[] QrPng);

// Função de um GHE com o CBO do cadastro e a quantidade de expostos (trabalhadores ativos da obra na função).
public record LinhaFuncaoPdf(int GheNumero, string Nome, string? Cbo, int Expostos);

public record RevisaoPdf(int Numero, DateTime Data, string Motivo, string? Responsavel);

// Curso obrigatório de uma função de um GHE, para o cronograma de treinamentos do PGR (NR-01, item 1.7).
// Aprovado = o engenheiro já confirmou o curso para a função na obra; senão ainda é sugestão do sistema.
public record LinhaTreinamentoPdf(
    int GheNumero, string Funcao, string Curso, string? Norma, int CargaHorariaMinima, int ValidadeEmMeses, string? Motivo, bool Aprovado);

public record ArquivoPgrPdf(byte[] Conteudo, string NomeArquivo);

public record GerarPdfGeracaoPgrEapQuery(Guid Id, bool Minuta = false) : IRequest<ArquivoPgrPdf>;

public class GerarPdfGeracaoPgrEapQueryHandler : IRequestHandler<GerarPdfGeracaoPgrEapQuery, ArquivoPgrPdf>
{
    private readonly IAppDbContext _db;
    private readonly IPgrPdfService _pdf;
    private readonly IQrCodeDocumentoService? _qrCode;

    public GerarPdfGeracaoPgrEapQueryHandler(IAppDbContext db, IPgrPdfService pdf, IQrCodeDocumentoService? qrCode = null)
    {
        _db = db;
        _pdf = pdf;
        _qrCode = qrCode;
    }

    public async Task<ArquivoPgrPdf> Handle(GerarPdfGeracaoPgrEapQuery request, CancellationToken ct)
    {
        var g = await SerializacaoGeracao.CarregarAsync(_db, request.Id, ct);
        if (request.Minuta)
        {
            if (g.Status == StatusGeracaoPgrEap.Aprovado)
                throw new InvalidOperationException("Este PGR já foi aprovado: baixe o PDF assinado.");
        }
        else if (g.Status != StatusGeracaoPgrEap.Aprovado)
            throw new InvalidOperationException("O PDF do PGR só é gerado depois da aprovação do Engenheiro de Segurança.");

        var obra = await _db.Obras.Where(o => o.Id == g.ObraId)
            .Select(o => new { o.Nome, o.Cnpj, o.Endereco, o.Cidade, o.Uf, o.LogoConteudo })
            .FirstOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Obra não encontrada.");
        var estrutura = SerializacaoGeracao.Ler(g.EstruturaJson);
        var rascunho = request.Minuta && g.Status is StatusGeracaoPgrEap.Rascunho or StatusGeracaoPgrEap.Devolvido;
        var sufixo = !request.Minuta ? string.Empty : rascunho ? " - RASCUNHO" : " - MINUTA";
        var nomeArquivo = $"PGR - {obra.Nome} - {g.DataElaboracao:yyyyMMdd}{sufixo}.pdf";
        var documentoMotor = request.Minuta
            ? null
            : await _db.DocumentosAssinatura.FirstOrDefaultAsync(d => d.EntidadeTipo == AssinaturaGeracaoPgr.EntidadeTipo && d.EntidadeId == g.Id, ct);
        // PDF assinado e finalizado é prova congelada: o download entrega sempre o mesmo arquivo, e não um PDF refeito.
        if (documentoMotor is { Status: StatusDocumentoAssinatura.Finalizado, PdfConteudo.Length: > 0 })
            return new ArquivoPgrPdf(documentoMotor.PdfConteudo!, nomeArquivo);

        // CBO do cadastro (por id, ou pelo nome quando a função ainda não tinha id) e expostos = trabalhadores
        // ativos da obra naquela função.
        var funcoesCadastro = await _db.Funcoes.Select(f => new { f.Id, f.Nome, f.CboCodigo }).ToListAsync(ct);
        var porNome = new Dictionary<string, (Guid Id, string? Cbo)>();
        foreach (var f in funcoesCadastro) porNome.TryAdd(FuncaoSstClassifier.Normalizar(f.Nome), (f.Id, f.CboCodigo));
        var expostosPorFuncao = (await _db.Trabalhadores.Where(t => t.ObraId == g.ObraId && t.DataDemissao == null)
                .Select(t => t.FuncaoId).ToListAsync(ct))
            .GroupBy(id => id).ToDictionary(x => x.Key, x => x.Count());
        // CBO da tabela oficial escolhido no PGR (29/10/2026), sempre como 0000-00; fora da tabela sai "—". Sem a tabela
        // carregada, vale o cadastro como antes.
        if (g.Status != StatusGeracaoPgrEap.Aprovado) await AjudanteGeral.UnificarAsync(_db, estrutura, ct);
        var catalogo = await CatalogoCbo.CarregarAsync(_db, ct);
        if (!catalogo.Vazio) await CboFuncoesGeracao.CompletarAsync(_db, estrutura, catalogo, ct);
        // Ajudante Geral conta também quem está no cadastro com outro nome de ajudante genérico ("Ajudante/Servente",
        // "Servente"...), enquanto o RH não renomeia o cargo.
        var idsAjudantes = funcoesCadastro.Where(f => AjudanteGeral.EhGenerico(f.Nome)).Select(f => f.Id).ToHashSet();
        var expostosAjudanteGeral = expostosPorFuncao.Where(x => idsAjudantes.Contains(x.Key)).Sum(x => x.Value);
        var linhasFuncao = new List<LinhaFuncaoPdf>();
        var funcoesDosGhes = new List<(int Ghe, string Nome, Guid Id)>();
        foreach (var ghe in estrutura.Ghes.OrderBy(x => x.Numero))
            foreach (var f in ghe.Funcoes)
            {
                Guid? id = f.FuncaoId;
                string? cbo = id is { } fid ? funcoesCadastro.FirstOrDefault(c => c.Id == fid)?.CboCodigo : null;
                if (id is null && porNome.TryGetValue(FuncaoSstClassifier.Normalizar(f.Nome), out var achada))
                    (id, cbo) = (achada.Id, achada.Cbo);
                if (!catalogo.Vazio) cbo = catalogo.Existe(f.Cbo) ? CodigoCbo.Formatar(CodigoCbo.Normalizar(f.Cbo)!) : null;
                linhasFuncao.Add(new LinhaFuncaoPdf(ghe.Numero, f.Nome, string.IsNullOrWhiteSpace(cbo) ? null : cbo.Trim(),
                    f.Nome == AjudanteGeral.Nome ? expostosAjudanteGeral : id is { } i ? expostosPorFuncao.GetValueOrDefault(i) : 0));
                if (id is { } idFuncao) funcoesDosGhes.Add((ghe.Numero, f.Nome, idFuncao));
            }
        var treinamentos = await CronogramaTreinamentosPgr.MontarAsync(_db, g.ObraId, funcoesDosGhes, ct);

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

        var assinaturas = await MontarAssinaturasAsync(estrutura, documentoMotor, ct);
        VerificacaoPgrPdf? verificacao = null;
        if (documentoMotor?.TokenValidacaoPublica is { } token && _qrCode is not null)
        {
            var qr = _qrCode.Gerar(token);
            verificacao = new VerificacaoPgrPdf(token, qr.UrlValidacao, qr.Png);
        }

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
            await SerializacaoGeracao.CarregarEpisAsync(_db, estrutura, ct),
            treinamentos,
            assinaturas,
            verificacao,
            request.Minuta,
            rascunho);

        return new ArquivoPgrPdf(_pdf.Gerar(dados, obra.LogoConteudo), nomeArquivo);
    }

    // As duas linhas de assinatura, com quem assina e, para quem já assinou no sistema, quando. Nulo quando o rascunho é de
    // antes da escolha de quem assina: o PDF mantém as linhas em branco de sempre.
    private async Task<List<AssinaturaPgrPdf>?> MontarAssinaturasAsync(EstruturaGeracaoPgr estrutura, DocumentoAssinatura? documento, CancellationToken ct)
    {
        var comp = estrutura.Complementares;
        if (comp?.Tecnico is null && comp?.Engenheiro is null)
            return null;

        var assinados = documento is null
            ? new Dictionary<PapelAssinatura, DateTime>()
            : (await _db.DocumentoSignatarios.Where(s => s.DocumentoAssinaturaId == documento.Id && s.Papel != null)
                    .Select(s => new { Papel = s.Papel!.Value, s.AssinadoEm }).ToListAsync(ct))
                .GroupBy(s => s.Papel).ToDictionary(g => g.Key, g => g.Min(x => x.AssinadoEm));

        AssinaturaPgrPdf? Montar(AssinanteGeracaoPgr? a, PapelAssinatura papel, string funcao, string descricao)
        {
            if (a is null) return null;
            return new AssinaturaPgrPdf(funcao, a.Nome, string.IsNullOrWhiteSpace(a.Registro) ? null : a.Registro.Trim(), descricao,
                assinados.TryGetValue(papel, out var quando) ? quando : null, a.AMao);
        }

        return new[]
        {
            Montar(comp?.Tecnico, PapelAssinatura.TecnicoSegurancaPgr, "Técnico de Segurança da obra",
                "Técnico de Segurança da obra · responsável pela implantação do PGR"),
            Montar(comp?.Engenheiro, PapelAssinatura.EngenheiroSegurancaPgr, "Engenheiro de Segurança do Trabalho",
                "Engenheiro de Segurança do Trabalho · responsável técnico pela elaboração do PGR"),
        }.OfType<AssinaturaPgrPdf>().ToList();
    }
}
