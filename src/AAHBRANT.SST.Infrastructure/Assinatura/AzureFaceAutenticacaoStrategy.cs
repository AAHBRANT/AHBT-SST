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
// IHttpClientFactory, mesmo estilo já usado por TelegramBotService — sem SDK do Azure como
// dependência nova. Confirme a versão da API (face/v1.0) contra a documentação da Azure no momento
// de rodar isto pela primeira vez contra um recurso real.
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
            throw new InvalidOperationException("Trabalhador ainda não confirmou o Termo de Aceite ou o consentimento de biometria.");

        var obra = await _db.Obras.FirstOrDefaultAsync(o => o.Id == trabalhador.ObraId, ct)
            ?? throw new KeyNotFoundException("Obra do trabalhador não encontrada.");

        using var cliente = CriarCliente();

        var personGroupId = obra.AzureFacePersonGroupId;
        if (personGroupId is null)
        {
            personGroupId = $"obra-{obra.Id:N}";
            await CriarPersonGroupSeNaoExistirAsync(cliente, personGroupId, obra.Nome, ct);
            obra.AzureFacePersonGroupId = personGroupId;
        }

        var personId = trabalhador.AzureFacePersonId;
        if (personId is null)
        {
            personId = await CriarPersonAsync(cliente, personGroupId, trabalhador.Nome, ct);
            trabalhador.AzureFacePersonId = personId;
        }

        await AdicionarFaceAsync(cliente, personGroupId, personId, fotoJpeg, ct);
        await _db.SaveChangesAsync(ct);

        await TreinarEAguardarAsync(cliente, personGroupId, ct);
    }

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

    private static async Task AdicionarFaceAsync(HttpClient cliente, string personGroupId, string personId, byte[] fotoJpeg, CancellationToken ct)
    {
        using var conteudo = new ByteArrayContent(fotoJpeg);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var resposta = await cliente.PostAsync($"face/v1.0/persongroups/{personGroupId}/persons/{personId}/persistedFaces", conteudo, ct);
        if (!resposta.IsSuccessStatusCode)
            throw new InvalidOperationException($"Falha ao adicionar foto ao Person no Azure Face API: {resposta.StatusCode}");
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
        var resposta = await EnviarComRetry429Async(() =>
        {
            using var conteudo = new ByteArrayContent(fotoJpeg);
            conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return cliente.PostAsync("face/v1.0/detect?returnFaceId=true", conteudo, ct);
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
            throw new InvalidOperationException($"Falha ao identificar rosto no Azure Face API: {resposta.StatusCode}");
        var resultados = await resposta.Content.ReadFromJsonAsync<List<IdentificacaoResposta>>(cancellationToken: ct);
        var candidato = resultados?.FirstOrDefault()?.Candidates.FirstOrDefault();
        return candidato is null ? null : new CandidatoIdentificacao(candidato.PersonId, candidato.Confidence);
    }

    private record CandidatoIdentificacao(string PersonId, double Confidence);

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
