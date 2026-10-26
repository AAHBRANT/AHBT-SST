using System.Globalization;
using System.Text;
using AAHBRANT.SST.Application.LeituraIa;
using AAHBRANT.SST.Infrastructure.Integracao.SuporteIa;

namespace AAHBRANT.SST.Infrastructure.LeituraIa;

// Classificação de cada arquivo da pasta da terceirizada (23/10/2026). Uma chamada por arquivo: o texto
// das primeiras páginas (ou a imagem, quando é foto/JPG solto) e as listas de documentos exigidos e de
// cursos cadastrados, para a IA devolver o nome exato de um deles.
public class ClaudeClassificadorDocumentoTerceirizado : IClassificadorDocumentoTerceirizadoIa
{
    // Certificado/ASO cabem com folga; contrato longo só precisa do começo para ser reconhecido.
    private const int MaximoCaracteres = 20_000;

    private const string Instrucoes = """
        Você organiza a pasta de documentos que uma empresa terceirizada entrega para trabalhar numa obra
        da AAHBRANT (construção civil, Brasil). Para UM arquivo, diga o que ele é e de quem é, usando só o
        que está escrito nele: não invente, não corrija, não complete. Campo ausente ou ilegível = null.
        Datas no formato AAAA-MM-DD. CPF e CNPJ como escritos.

        tipo:
        - "contrato": contrato de prestação de serviço/empreitada entre a AAHBRANT e a empresa;
        - "documento_empresa": documento da EMPRESA (não de uma pessoa), como cartão CNPJ, contrato social,
          PGR, PCMSO, certidões, apólice de seguro, alvará, ART, comprovante de recolhimento etc.;
        - "aso": Atestado de Saúde Ocupacional de um funcionário;
        - "certificado": certificado de treinamento/curso (NR-06, NR-10, NR-18, NR-35, integração etc.) de
          um funcionário;
        - "ficha_epi": ficha/termo de entrega de EPI de um funcionário;
        - "outro": qualquer outra coisa (RG, CTPS, foto, documento ilegível).

        documentoEmpresa: só quando tipo = "documento_empresa"; copie EXATAMENTE um nome da lista de
        documentos exigidos que corresponda ao documento; nenhum corresponde = null.
        curso: só quando tipo = "certificado"; copie EXATAMENTE um nome da lista de cursos que corresponda
        ao treinamento (mesma NR/assunto); nenhum corresponde = null.
        pessoaNome/pessoaCpf/pessoaFuncao: o funcionário a quem o documento pertence (ASO, certificado,
        ficha de EPI). Em documento da empresa ou contrato, null.
        empresaRazaoSocial/empresaCnpj: a empresa terceirizada (nunca a AAHBRANT), se aparecer.
        dataDocumento: data de emissão/realização (ASO: data do exame; certificado: data de conclusão).
        dataValidade: validade escrita no documento; se não houver, null (não calcule).
        asoTipo e asoResultado: só para ASO; nos demais, "nenhum".
        cargaHoraria e instituicao: só para certificado (horas inteiras e quem emitiu).
        resumo: uma frase curta descrevendo o documento (ex.: "ASO periódico de João da Silva").
        """;

    private record Lido(string Tipo, string? DocumentoEmpresa, string? Curso, string? PessoaNome, string? PessoaCpf,
        string? PessoaFuncao, string? EmpresaRazaoSocial, string? EmpresaCnpj, string? DataDocumento, string? DataValidade,
        string AsoTipo, string AsoResultado, string? MedicoNome, string? MedicoCrm, int? CargaHoraria, string? Instituicao,
        string? Resumo);

    private static object Texto() => new { type = new[] { "string", "null" } };
    private static object Opcoes(params string[] valores) => new { type = "string", @enum = valores };

    private static readonly Dictionary<string, object> Campos = new()
    {
        ["tipo"] = Opcoes("contrato", "documento_empresa", "aso", "certificado", "ficha_epi", "outro"),
        ["documentoEmpresa"] = Texto(),
        ["curso"] = Texto(),
        ["pessoaNome"] = Texto(),
        ["pessoaCpf"] = Texto(),
        ["pessoaFuncao"] = Texto(),
        ["empresaRazaoSocial"] = Texto(),
        ["empresaCnpj"] = Texto(),
        ["dataDocumento"] = Texto(),
        ["dataValidade"] = Texto(),
        ["asoTipo"] = Opcoes("nenhum", "admissional", "periodico", "retorno", "mudanca_funcao", "demissional"),
        ["asoResultado"] = Opcoes("nenhum", "apto", "apto_restricao", "inapto"),
        ["medicoNome"] = Texto(),
        ["medicoCrm"] = Texto(),
        ["cargaHoraria"] = new { type = new[] { "integer", "null" } },
        ["instituicao"] = Texto(),
        ["resumo"] = Texto(),
    };

    private static readonly object Schema = new
    {
        type = "object",
        properties = Campos,
        required = Campos.Keys.ToArray(),
        additionalProperties = false,
    };

    private readonly ClaudeChatJsonCliente _chat;

    public ClaudeClassificadorDocumentoTerceirizado(ClaudeChatJsonCliente chat) => _chat = chat;

    public async Task<DocumentoTerceirizadoLidoIa> ClassificarAsync(string nomeArquivo, IReadOnlyList<string> paginas, byte[]? imagem,
        IReadOnlyList<string> documentosEmpresa, IReadOnlyList<string> cursos, CancellationToken ct)
    {
        var mensagem = new StringBuilder();
        mensagem.AppendLine($"Nome do arquivo: {nomeArquivo}");
        mensagem.AppendLine();
        mensagem.AppendLine("Documentos exigidos da empresa (lista):");
        foreach (var d in documentosEmpresa) mensagem.AppendLine($"- {d}");
        mensagem.AppendLine();
        mensagem.AppendLine("Cursos cadastrados (lista):");
        foreach (var c in cursos) mensagem.AppendLine($"- {c}");
        mensagem.AppendLine();

        IReadOnlyList<string>? imagens = null;
        if (imagem is not null)
        {
            var jpeg = ImagemPaginaPdf.Reduzir(imagem);
            imagens = new[] { $"data:image/jpeg;base64,{Convert.ToBase64String(jpeg)}" };
            mensagem.AppendLine("O documento é a imagem anexa.");
        }
        else
        {
            var texto = new StringBuilder();
            for (var i = 0; i < paginas.Count; i++)
                texto.AppendLine($"=== PÁGINA {i + 1} ===").AppendLine(paginas[i]);
            var conteudo = texto.ToString();
            mensagem.AppendLine("Texto do documento:");
            mensagem.AppendLine(conteudo.Length > MaximoCaracteres ? conteudo[..MaximoCaracteres] : conteudo);
        }

        var l = await _chat.ObterAsync<Lido>(Instrucoes, mensagem.ToString(), "documento_terceirizado", Schema, ct,
            maxTokens: 1500, timeoutSegundos: 90, imagens: imagens);

        return new DocumentoTerceirizadoLidoIa(l.Tipo, l.DocumentoEmpresa, l.Curso, l.PessoaNome, l.PessoaCpf, l.PessoaFuncao,
            l.EmpresaRazaoSocial, l.EmpresaCnpj, Data(l.DataDocumento), Data(l.DataValidade), l.AsoTipo, l.AsoResultado,
            l.MedicoNome, l.MedicoCrm, l.CargaHoraria, l.Instituicao, l.Resumo);
    }

    private static DateTime? Data(string? texto) =>
        DateTime.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d)
            ? DateTime.SpecifyKind(d.Date, DateTimeKind.Utc) : null;
}
