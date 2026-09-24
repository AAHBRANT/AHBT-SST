using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Assinatura;

// Estratégia de autenticação facial via Azure Face API — mesmo papel de FutronicAutenticacaoStrategy,
// mas o match acontece na nuvem (Face - Identify), não no dispositivo. Chamadas REST cruas via
// IHttpClientFactory — sem SDK do Azure como dependência nova. Confirme a versão da API
// (face/v1.0) contra a documentação da Azure no momento de rodar isto pela primeira vez contra um
// recurso real.
public class AzureFaceAutenticacaoStrategy : IAutenticacaoFacialService
{
    private readonly IAppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AssinaturaOptions _options;

    public AzureFaceAutenticacaoStrategy(IAppDbContext db, IHttpClientFactory httpClientFactory, IOptions<AssinaturaOptions> options)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task CadastrarAsync(Guid trabalhadorId, byte[] fotoJpeg, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == trabalhadorId, ct)
            ?? throw new KeyNotFoundException("Trabalhador não encontrado.");

        if (trabalhador.TermoAceiteAssinaturaEletronicaEm is null || trabalhador.ConsentimentoBiometriaEm is null)
            throw new InvalidOperationException("Trabalhador ainda não confirmou o Termo de Aceite de Assinatura Eletrônica e o consentimento LGPD para uso de biometria facial.");

        var obra = await _db.Obras.FirstOrDefaultAsync(o => o.Id == trabalhador.ObraId, ct)
            ?? throw new KeyNotFoundException("Obra do trabalhador não encontrada.");

        using var cliente = CriarCliente();

        // A foto de cadastro é a régua de todo reconhecimento futuro: uma referência ruim derruba a
        // confiança de todas as tentativas de assinatura depois, e o técnico não tem como adivinhar
        // que o problema está no cadastro, não na hora de assinar. Por isso a recusa acontece aqui,
        // com o trabalhador ainda na frente da câmera e podendo repetir na hora (pedido do usuário,
        // 24/09) — em vez de aceitar qualquer imagem e descobrir o problema semanas depois.
        await ValidarQualidadeParaCadastroAsync(cliente, fotoJpeg, ct);

        var personGroupId = obra.AzureFacePersonGroupId;
        if (personGroupId is null)
        {
            personGroupId = $"obra-{obra.Id:N}";
            obra.AzureFacePersonGroupId = personGroupId;
        }
        await CriarPersonGroupSeNaoExistirAsync(cliente, personGroupId, obra.Nome, ct);

        var personId = trabalhador.AzureFacePersonId;
        if (personId is null)
        {
            personId = await CriarPersonAsync(cliente, personGroupId, trabalhador.Nome, ct);
            trabalhador.AzureFacePersonId = personId;
        }

        var erroAdicionarFace = await TentarAdicionarFaceAsync(cliente, personGroupId, personId, fotoJpeg, ct);
        if (erroAdicionarFace?.Code is "PersonNotFound")
        {
            personId = await CriarPersonAsync(cliente, personGroupId, trabalhador.Nome, ct);
            trabalhador.AzureFacePersonId = personId;
            erroAdicionarFace = await TentarAdicionarFaceAsync(cliente, personGroupId, personId, fotoJpeg, ct);
        }
        if (erroAdicionarFace is not null)
            throw new InvalidOperationException(MontarMensagemErroAzure("Falha ao adicionar foto ao Person no Azure Face API", erroAdicionarFace));

        await _db.SaveChangesAsync(ct);

        await TreinarEAguardarAsync(cliente, personGroupId, ct);
    }

    // Resolução mínima da imagem inteira. Abaixo disto nem vale gastar chamada no Azure: câmera de
    // notebook antigo e print de tela caem aqui.
    private const int LadoMinimoImagemPx = 480;

    // Lado mínimo do rosto detectado dentro da foto. A Microsoft recomenda 200x200 para a foto de
    // referência; abaixo disso a pessoa está longe demais da câmera, ainda que a imagem seja grande.
    private const int LadoMinimoRostoPx = 200;

    // Recusa foto ruim no momento do cadastro, com a mensagem dizendo o que corrigir. Valida em três
    // camadas, da mais barata para a mais cara:
    //   1. resolução da imagem, sem sair da máquina;
    //   2. um rosto e apenas um, com tamanho suficiente;
    //   3. qualityForRecognition do próprio Azure, que é o que ele usa para dizer se a imagem serve
    //      como referência de reconhecimento.
    //
    // O detect aqui vai com returnFaceId=false de propósito: não precisamos do id nesta chamada, e
    // assim ela não depende da aprovação de Limited Access. O recognitionModel é exigido pelo Azure
    // para calcular qualityForRecognition e não interfere no modelo do PersonGroup — esta chamada só
    // mede a foto, quem cadastra de fato é o TentarAdicionarFaceAsync logo depois.
    private static async Task ValidarQualidadeParaCadastroAsync(HttpClient cliente, byte[] fotoJpeg, CancellationToken ct)
    {
        using (var imagem = SixLabors.ImageSharp.Image.Load(fotoJpeg))
        {
            if (Math.Min(imagem.Width, imagem.Height) < LadoMinimoImagemPx)
                throw new InvalidOperationException(
                    $"A foto está em resolução baixa demais para servir de referência ({imagem.Width}x{imagem.Height}). " +
                    $"Use uma câmera melhor ou aproxime-se: o menor lado precisa ter pelo menos {LadoMinimoImagemPx} pixels.");
        }

        // `async` + `await` aqui não é enfeite: sem eles o `using` fecha o ByteArrayContent antes de
        // a requisição terminar de enviá-lo (mesmo padrão já usado em DetectarRostosAsync).
        var resposta = await EnviarComRetry429Async(async () =>
        {
            using var conteudo = new ByteArrayContent(fotoJpeg);
            conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return await cliente.PostAsync(
                "face/v1.0/detect?returnFaceId=false&detectionModel=detection_01&recognitionModel=recognition_04&returnFaceAttributes=qualityForRecognition",
                conteudo, ct);
        }, ct);

        if (!resposta.IsSuccessStatusCode)
            throw new InvalidOperationException($"Falha ao avaliar a qualidade da foto no Azure Face API: {resposta.StatusCode}");

        var rostos = await resposta.Content.ReadFromJsonAsync<List<RostoComQualidadeResposta>>(cancellationToken: ct)
            ?? new List<RostoComQualidadeResposta>();

        if (rostos.Count == 0)
            throw new InvalidOperationException(
                "Nenhum rosto foi detectado na foto. Enquadre o rosto de frente, com o ambiente bem iluminado e sem nada cobrindo o rosto.");

        if (rostos.Count > 1)
            throw new InvalidOperationException(
                "Mais de um rosto na foto. A foto de cadastro precisa ter apenas o trabalhador — peça para os outros saírem do enquadramento.");

        var rosto = rostos[0];
        var ladoRosto = Math.Min(rosto.FaceRectangle.Width, rosto.FaceRectangle.Height);
        if (ladoRosto < LadoMinimoRostoPx)
            throw new InvalidOperationException(
                $"O rosto ficou pequeno demais na foto ({ladoRosto} pixels). Aproxime o rosto da câmera até ele ocupar boa parte do quadro.");

        // "high" é o único nível que a Microsoft considera adequado para foto de referência.
        var qualidade = rosto.FaceAttributes?.QualityForRecognition;
        if (!string.Equals(qualidade, "high", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "A qualidade da foto não está boa o suficiente para reconhecimento" +
                (qualidade is null ? "" : $" (avaliada como \"{TraduzirQualidade(qualidade)}\")") +
                ". Melhore a iluminação (luz de frente, sem contraluz), tire boné e óculos escuros, olhe direto para a câmera e evite foto tremida.");
    }

    private static string TraduzirQualidade(string qualidade) => qualidade.ToLowerInvariant() switch
    {
        "low" => "baixa",
        "medium" => "média",
        _ => qualidade,
    };

    public async Task<ResultadoIdentificacaoFacial> IdentificarAsync(Guid obraId, byte[] fotoJpeg, CancellationToken ct)
    {
        var obra = await _db.Obras.FirstOrDefaultAsync(o => o.Id == obraId, ct)
            ?? throw new KeyNotFoundException("Obra não encontrada.");

        if (!obra.MetodosAutenticacaoHabilitados.HasFlag(MetodoAutenticacaoObra.ReconhecimentoFacial))
            throw new InvalidOperationException("Este método de assinatura não está habilitado para a obra deste trabalhador.");

        if (obra.AzureFacePersonGroupId is null)
            return new ResultadoIdentificacaoFacial(false, null, MotivoRejeicaoFacial.RostoNaoReconhecido, null);

        using var cliente = CriarCliente();

        var faceIds = await DetectarRostosAsync(cliente, fotoJpeg, ct);
        if (faceIds.Count == 0)
            return new ResultadoIdentificacaoFacial(false, null, MotivoRejeicaoFacial.NenhumRostoDetectado, null);
        if (faceIds.Count > 1)
            return new ResultadoIdentificacaoFacial(false, null, MotivoRejeicaoFacial.MultiplosRostosDetectados, null);

        var candidato = await IdentificarRostoAsync(cliente, obra.AzureFacePersonGroupId, faceIds[0], ct);
        if (candidato is null)
            return new ResultadoIdentificacaoFacial(false, null, MotivoRejeicaoFacial.RostoNaoReconhecido, null);

        if (candidato.Confidence < _options.LimiarConfiancaFacialMinimo)
            return new ResultadoIdentificacaoFacial(false, null, MotivoRejeicaoFacial.RostoNaoReconhecido, candidato.Confidence);
        if (candidato.Confidence < _options.LimiarConfiancaFacial)
            return new ResultadoIdentificacaoFacial(false, null, MotivoRejeicaoFacial.ConfiancaBaixa, candidato.Confidence);

        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.AzureFacePersonId == candidato.PersonId && t.ObraId == obraId, ct);
        if (trabalhador is null)
            return new ResultadoIdentificacaoFacial(false, null, MotivoRejeicaoFacial.RostoNaoReconhecido, candidato.Confidence);

        var resultado = new ResultadoAutenticacaoAssinatura(trabalhador.Id, MetodoAutenticacaoAssinatura.ReconhecimentoFacial);
        return new ResultadoIdentificacaoFacial(true, resultado, null, candidato.Confidence);
    }

    private HttpClient CriarCliente()
    {
        if (string.IsNullOrWhiteSpace(_options.AzureFaceApiEndpoint) || string.IsNullOrWhiteSpace(_options.AzureFaceApiKey))
            throw new InvalidOperationException("Azure Face API não está configurada (Assinatura:AzureFaceApiEndpoint/AzureFaceApiKey).");

        var cliente = _httpClientFactory.CreateClient();
        cliente.BaseAddress = new Uri(_options.AzureFaceApiEndpoint.TrimEnd('/') + "/");
        cliente.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _options.AzureFaceApiKey);
        return cliente;
    }

    private static async Task CriarPersonGroupSeNaoExistirAsync(HttpClient cliente, string personGroupId, string nomeObra, CancellationToken ct)
    {
        var resposta = await cliente.PutAsJsonAsync($"face/v1.0/persongroups/{personGroupId}", new { name = nomeObra }, ct);
        // 409 = já existe (ex.: outra instância criou entre a checagem e aqui) — tratado como sucesso.
        if (!resposta.IsSuccessStatusCode && resposta.StatusCode != System.Net.HttpStatusCode.Conflict)
            throw new InvalidOperationException($"Falha ao criar PersonGroup no Azure Face API: {resposta.StatusCode}");
    }

    private static async Task<string> CriarPersonAsync(HttpClient cliente, string personGroupId, string nomeTrabalhador, CancellationToken ct)
    {
        var resposta = await cliente.PostAsJsonAsync($"face/v1.0/persongroups/{personGroupId}/persons", new { name = nomeTrabalhador }, ct);
        if (!resposta.IsSuccessStatusCode)
            throw new InvalidOperationException($"Falha ao criar Person no Azure Face API: {resposta.StatusCode}");
        var corpo = await resposta.Content.ReadFromJsonAsync<PersonCriadoResposta>(cancellationToken: ct);
        return corpo!.PersonId;
    }

    private static async Task<AzureFaceErro?> TentarAdicionarFaceAsync(HttpClient cliente, string personGroupId, string personId, byte[] fotoJpeg, CancellationToken ct)
    {
        using var conteudo = new ByteArrayContent(fotoJpeg);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var resposta = await cliente.PostAsync($"face/v1.0/persongroups/{personGroupId}/persons/{personId}/persistedFaces", conteudo, ct);
        if (resposta.IsSuccessStatusCode)
            return null;
        return await LerErroAzureAsync(resposta, ct);
    }

    private static async Task TreinarEAguardarAsync(HttpClient cliente, string personGroupId, CancellationToken ct)
    {
        var respostaTreino = await cliente.PostAsync($"face/v1.0/persongroups/{personGroupId}/train", content: null, ct);
        if (!respostaTreino.IsSuccessStatusCode)
            throw new InvalidOperationException($"Falha ao disparar treino no Azure Face API: {respostaTreino.StatusCode}");

        // Treino é assíncrono no Azure — poll com backoff curto (ação administrativa pontual, ok
        // bloquear por alguns segundos). 10 tentativas de 1s cobre o caso comum (grupo pequeno).
        for (var tentativa = 0; tentativa < 10; tentativa++)
        {
            await Task.Delay(1000, ct);
            var status = await cliente.GetFromJsonAsync<TreinoStatusResposta>($"face/v1.0/persongroups/{personGroupId}/training", ct);
            if (status?.Status == "succeeded") return;
            if (status?.Status == "failed")
                throw new InvalidOperationException($"Treino do PersonGroup falhou no Azure Face API: {status.Message}");
        }
        throw new InvalidOperationException("Treino do PersonGroup no Azure Face API não concluiu a tempo.");
    }

    // Retry curto só para os dois caminhos "quentes" de assinatura (Detect/Identify) — é aqui que o
    // limite de 20 chamadas/minuto do tier F0 pode ser atingido de verdade (várias pessoas assinando
    // em sequência rápida, ex.: DDS matinal). CadastrarAsync (enrollment) não precisa: é ação pontual
    // e já espera segundos no polling do treino.
    private static async Task<HttpResponseMessage> EnviarComRetry429Async(Func<Task<HttpResponseMessage>> enviar, CancellationToken ct)
    {
        for (var tentativa = 0; ; tentativa++)
        {
            var resposta = await enviar();
            if (resposta.StatusCode != (System.Net.HttpStatusCode)429 || tentativa >= 2)
                return resposta;
            resposta.Dispose();
            await Task.Delay(TimeSpan.FromSeconds(1 + tentativa), ct);
        }
    }

    private static async Task<List<string>> DetectarRostosAsync(HttpClient cliente, byte[] fotoJpeg, CancellationToken ct)
    {
        var resposta = await EnviarComRetry429Async(async () =>
        {
            using var conteudo = new ByteArrayContent(fotoJpeg);
            conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return await cliente.PostAsync("face/v1.0/detect?returnFaceId=true", conteudo, ct);
        }, ct);
        if (!resposta.IsSuccessStatusCode)
            throw new InvalidOperationException($"Falha ao detectar rostos no Azure Face API: {resposta.StatusCode}");
        var rostos = await resposta.Content.ReadFromJsonAsync<List<RostoDetectadoResposta>>(cancellationToken: ct);
        return rostos?.Select(r => r.FaceId).ToList() ?? new List<string>();
    }

    private static async Task<CandidatoIdentificacao?> IdentificarRostoAsync(HttpClient cliente, string personGroupId, string faceId, CancellationToken ct)
    {
        var corpo = new { personGroupId, faceIds = new[] { faceId }, maxNumOfCandidatesReturned = 1, confidenceThreshold = 0.5 };
        var resposta = await EnviarComRetry429Async(() => cliente.PostAsJsonAsync("face/v1.0/identify", corpo, ct), ct);
        if (!resposta.IsSuccessStatusCode)
        {
            var erro = await LerErroAzureAsync(resposta, ct);
            if (erro.Code is "PersonGroupNotFound" or "LargePersonGroupNotFound")
                return null;
            throw new InvalidOperationException(MontarMensagemErroAzure("Falha ao identificar rosto no Azure Face API", erro));
        }
        var resultados = await resposta.Content.ReadFromJsonAsync<List<IdentificacaoResposta>>(cancellationToken: ct);
        var candidato = resultados?.FirstOrDefault()?.Candidates.FirstOrDefault();
        return candidato is null ? null : new CandidatoIdentificacao(candidato.PersonId, candidato.Confidence);
    }

    private static async Task<AzureFaceErro> LerErroAzureAsync(HttpResponseMessage resposta, CancellationToken ct)
    {
        AzureFaceErroResposta? corpo = null;
        try
        {
            corpo = await resposta.Content.ReadFromJsonAsync<AzureFaceErroResposta>(cancellationToken: ct);
        }
        catch
        {
            // Alguns erros de gateway podem não vir no envelope JSON padrão do Face API.
        }

        var erro = corpo?.Error;
        var codigo = erro?.InnerError?.Code ?? erro?.Code ?? resposta.StatusCode.ToString();
        var mensagem = erro?.InnerError?.Message ?? erro?.Message ?? resposta.ReasonPhrase ?? resposta.StatusCode.ToString();
        return new AzureFaceErro(resposta.StatusCode.ToString(), codigo, mensagem);
    }

    private static string MontarMensagemErroAzure(string contexto, AzureFaceErro erro)
        => $"{contexto}: {erro.Status} ({erro.Code}: {erro.Message})";

    private record CandidatoIdentificacao(string PersonId, double Confidence);
    private record AzureFaceErro(string Status, string Code, string Message);

    private class AzureFaceErroResposta
    {
        [JsonPropertyName("error")]
        public AzureFaceErroCorpo? Error { get; set; }
    }

    private class AzureFaceErroCorpo
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }
        [JsonPropertyName("message")]
        public string? Message { get; set; }
        [JsonPropertyName("innererror")]
        public AzureFaceErroCorpo? InnerError { get; set; }
    }

    private class PersonCriadoResposta
    {
        [JsonPropertyName("personId")]
        public string PersonId { get; set; } = "";
    }

    private class TreinoStatusResposta
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = "";
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    private class RostoDetectadoResposta
    {
        [JsonPropertyName("faceId")]
        public string FaceId { get; set; } = "";
    }

    // Resposta do detect usado só para medir a qualidade da foto no cadastro — ver
    // ValidarQualidadeParaCadastroAsync.
    private class RostoComQualidadeResposta
    {
        [JsonPropertyName("faceRectangle")]
        public RetanguloRosto FaceRectangle { get; set; } = new();

        [JsonPropertyName("faceAttributes")]
        public AtributosRosto? FaceAttributes { get; set; }
    }

    private class RetanguloRosto
    {
        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }
    }

    private class AtributosRosto
    {
        // "low" | "medium" | "high" — a Microsoft recomenda aceitar apenas "high" no cadastro,
        // porque é a foto de referência que define a qualidade de todo reconhecimento futuro.
        [JsonPropertyName("qualityForRecognition")]
        public string? QualityForRecognition { get; set; }
    }

    private class IdentificacaoResposta
    {
        [JsonPropertyName("candidates")]
        public List<CandidatoResposta> Candidates { get; set; } = new();
    }

    private class CandidatoResposta
    {
        [JsonPropertyName("personId")]
        public string PersonId { get; set; } = "";
        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }
    }
}
