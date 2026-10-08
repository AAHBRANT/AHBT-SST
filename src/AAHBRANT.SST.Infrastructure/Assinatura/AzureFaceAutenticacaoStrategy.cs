using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
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

        // Regra do usuário (30/09): o facial é cadastrado uma única vez. A foto só é gravada depois de
        // aprovada na qualidade e aceita pelo Azure, então existir foto = cadastro concluído com sucesso.
        if (await _db.FotosCadastroFacial.AnyAsync(f => f.TrabalhadorId == trabalhadorId, ct))
            throw new InvalidOperationException("O reconhecimento facial deste trabalhador já está cadastrado.");

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

        // Só chega aqui com a foto já aprovada na validação de qualidade e aceita pelo Azure: é essa
        // foto (a referência do reconhecimento) que fica guardada no perfil do trabalhador.
        _db.FotosCadastroFacial.Add(new FotoCadastroFacial
        {
            TrabalhadorId = trabalhador.Id,
            Conteudo = fotoJpeg,
            ContentType = "image/jpeg",
            HashSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(fotoJpeg)).ToLowerInvariant(),
            CapturadaEm = DateTime.UtcNow,
        });

        // Decisão do usuário (07/10): a foto do cadastro/recadastro facial vira a foto do perfil,
        // substituindo a anterior. É uma cópia (a foto biométrica segue guardada à parte, com hash, para
        // auditoria) e é gravada no mesmo SaveChanges: ou as duas mudam, ou nenhuma. Obs.: a foto do
        // perfil também aparece na página pública de identificação e no G-RH.
        trabalhador.FotoConteudo = fotoJpeg;
        trabalhador.FotoContentType = "image/jpeg";

        await _db.SaveChangesAsync(ct);

        await TreinarEAguardarAsync(cliente, personGroupId, ct);
    }

    public async Task RemoverCadastroAsync(Guid trabalhadorId, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == trabalhadorId, ct)
            ?? throw new KeyNotFoundException("Trabalhador não encontrado.");
        if (trabalhador.AzureFacePersonId is null)
            return;

        var obra = await _db.Obras.FirstOrDefaultAsync(o => o.Id == trabalhador.ObraId, ct)
            ?? throw new KeyNotFoundException("Obra do trabalhador não encontrada.");
        if (obra.AzureFacePersonGroupId is null)
            return;

        using var cliente = CriarCliente();

        var resposta = await cliente.DeleteAsync($"face/v1.0/persongroups/{obra.AzureFacePersonGroupId}/persons/{trabalhador.AzureFacePersonId}", ct);
        // 404 = a pessoa (ou o grupo) já não existe no Azure: o objetivo da chamada já está cumprido.
        if (!resposta.IsSuccessStatusCode && resposta.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            var erro = await LerErroAzureAsync(resposta, ct);
            throw new InvalidOperationException(MontarMensagemErroAzure("Falha ao apagar o cadastro facial no Azure Face API", erro));
        }

        // O retreino não é requisito para a remoção valer: mesmo que o índice antigo ainda devolva o
        // id apagado, IdentificarAsync não encontra trabalhador com esse PersonId e rejeita. Por isso
        // uma falha aqui (ex.: grupo ficou sem ninguém) não desfaz a remoção.
        try
        {
            await TreinarEAguardarAsync(cliente, obra.AzureFacePersonGroupId, ct);
        }
        catch (InvalidOperationException)
        {
        }
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

    public async Task<ResultadoIdentificacaoFacial> IdentificarAsync(Guid obraId, byte[] fotoJpeg, CancellationToken ct, bool exigirMargemSobreSegundoColocado = false)
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

        var (candidato, confiancaSegundo) = await IdentificarRostoAsync(cliente, obra.AzureFacePersonGroupId, faceIds[0], ct);
        if (candidato is null)
            return await RejeitarAsync(obraId, MotivoRejeicaoFacial.RostoNaoReconhecido, null, null, ct);

        if (candidato.Confidence < _options.LimiarConfiancaFacialMinimo)
            return await RejeitarAsync(obraId, MotivoRejeicaoFacial.RostoNaoReconhecido, candidato.Confidence, null, ct);

        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.AzureFacePersonId == candidato.PersonId && t.ObraId == obraId, ct);

        // Confiança entre o mínimo e o limiar: o Azure tem quase certeza de quem é, e por isso é aqui
        // que a falha vira atribuível a um cadastro fraco.
        if (candidato.Confidence < _options.LimiarConfiancaFacial)
            return await RejeitarAsync(obraId, MotivoRejeicaoFacial.ConfiancaBaixa, candidato.Confidence, trabalhador?.Id, ct);

        if (trabalhador is null)
            return await RejeitarAsync(obraId, MotivoRejeicaoFacial.RostoNaoReconhecido, candidato.Confidence, null, ct);

        // Fila 1:N: ninguém escolhe a pessoa, então só confirma se o primeiro colocado estiver bem à
        // frente do segundo. Dois candidatos próximos = provável colega parecido, melhor usar a digital.
        if (exigirMargemSobreSegundoColocado && confiancaSegundo is { } segundo
            && candidato.Confidence - segundo < _options.MargemMinimaIdentificacaoFacial)
            return await RejeitarAsync(obraId, MotivoRejeicaoFacial.RostoAmbiguo, candidato.Confidence, null, ct);

        // Rastro da validação para o log de assinaturas: modelo real do grupo (lido do Azure), grupo e
        // id da requisição. Nunca bloqueia a assinatura: se não vier, o log mostra "não informado".
        var modelo = await ObterModeloReconhecimentoDoGrupoAsync(cliente, obra.AzureFacePersonGroupId, ct);
        var resultado = new ResultadoAutenticacaoAssinatura(
            trabalhador.Id, MetodoAutenticacaoAssinatura.ReconhecimentoFacial,
            ValidacaoModelo: modelo, ValidacaoGrupoId: obra.AzureFacePersonGroupId, ValidacaoRequisicaoId: candidato.RequisicaoId);
        return new ResultadoIdentificacaoFacial(true, resultado, null, candidato.Confidence);
    }

    // Registra a falha para a lista de cadastros fracos e devolve a rejeição. O registro é melhor esforço:
    // nunca atrapalha o fluxo de quem está assinando ou confirmando presença.
    private async Task<ResultadoIdentificacaoFacial> RejeitarAsync(Guid obraId, MotivoRejeicaoFacial motivo, double? confianca, Guid? trabalhadorId, CancellationToken ct)
    {
        try
        {
            _db.FalhasReconhecimentoFacial.Add(new FalhaReconhecimentoFacial
            {
                ObraId = obraId,
                TrabalhadorId = trabalhadorId,
                Motivo = motivo switch
                {
                    MotivoRejeicaoFacial.ConfiancaBaixa => MotivoFalhaFacial.ConfiancaBaixa,
                    MotivoRejeicaoFacial.RostoAmbiguo => MotivoFalhaFacial.RostoAmbiguo,
                    _ => MotivoFalhaFacial.RostoNaoReconhecido,
                },
                Confianca = confianca,
                OcorridaEm = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync(ct);
            await LimparFalhasAntigasSeHoraAsync(ct);
        }
        catch (Exception)
        {
            // Sem tratamento de propósito: perder um registro de falha não pode derrubar a identificação.
        }
        return new ResultadoIdentificacaoFacial(false, null, motivo, confianca, trabalhadorId);
    }

    // Retenção de 90 dias das falhas de reconhecimento (sem foto, mas ainda dado pessoal). Apaga de fato,
    // não só arquiva (por isso ExecuteDelete e IgnoreQueryFilters). Roda no máximo uma vez por hora por
    // instância, aproveitando o próprio registro de falhas, sem precisar de um job separado.
    private const int DiasDeRetencaoDasFalhas = 90;
    private static DateTime _ultimaLimpezaDasFalhas = DateTime.MinValue;

    private async Task LimparFalhasAntigasSeHoraAsync(CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        if (agora - _ultimaLimpezaDasFalhas < TimeSpan.FromHours(1)) return;
        _ultimaLimpezaDasFalhas = agora;

        var limite = agora.AddDays(-DiasDeRetencaoDasFalhas);
        try
        {
            await _db.FalhasReconhecimentoFacial.IgnoreQueryFilters().Where(f => f.OcorridaEm < limite).ExecuteDeleteAsync(ct);
        }
        catch (InvalidOperationException)
        {
            // Provedor sem suporte a ExecuteDelete (banco em memória dos testes): ignora.
        }
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

    // Devolve o melhor candidato e a confiança do segundo (quando houver), para a checagem de margem da fila.
    private static async Task<(CandidatoIdentificacao? Primeiro, double? ConfiancaSegundo)> IdentificarRostoAsync(HttpClient cliente, string personGroupId, string faceId, CancellationToken ct)
    {
        var corpo = new { personGroupId, faceIds = new[] { faceId }, maxNumOfCandidatesReturned = 2, confidenceThreshold = 0.5 };
        var resposta = await EnviarComRetry429Async(() => cliente.PostAsJsonAsync("face/v1.0/identify", corpo, ct), ct);
        if (!resposta.IsSuccessStatusCode)
        {
            var erro = await LerErroAzureAsync(resposta, ct);
            if (erro.Code is "PersonGroupNotFound" or "LargePersonGroupNotFound")
                return (null, null);
            throw new InvalidOperationException(MontarMensagemErroAzure("Falha ao identificar rosto no Azure Face API", erro));
        }
        var resultados = await resposta.Content.ReadFromJsonAsync<List<IdentificacaoResposta>>(cancellationToken: ct);
        var candidatos = resultados?.FirstOrDefault()?.Candidates.OrderByDescending(c => c.Confidence).ToList() ?? new List<CandidatoResposta>();
        var primeiro = candidatos.FirstOrDefault();
        return primeiro is null
            ? (null, null)
            : (new CandidatoIdentificacao(primeiro.PersonId, primeiro.Confidence, LerIdRequisicao(resposta)), candidatos.Skip(1).FirstOrDefault()?.Confidence);
    }

    // O Face API devolve o identificador da chamada no cabeçalho apim-request-id (ou x-ms-request-id);
    // é o que a Microsoft pede para localizar uma requisição.
    private static string? LerIdRequisicao(HttpResponseMessage resposta)
    {
        foreach (var cabecalho in new[] { "apim-request-id", "x-ms-request-id" })
            if (resposta.Headers.TryGetValues(cabecalho, out var valores) && valores.FirstOrDefault() is { Length: > 0 } valor)
                return valor;
        return null;
    }

    // O modelo que vale na identificação é o do grupo de pessoas (definido na criação do grupo), não o
    // recognition_04 usado só na checagem de qualidade do cadastro. Consulta uma vez por grupo e guarda.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> ModelosPorGrupo = new();

    private static async Task<string?> ObterModeloReconhecimentoDoGrupoAsync(HttpClient cliente, string personGroupId, CancellationToken ct)
    {
        if (ModelosPorGrupo.TryGetValue(personGroupId, out var emCache)) return emCache;
        try
        {
            var resposta = await cliente.GetAsync($"face/v1.0/persongroups/{personGroupId}", ct);
            if (!resposta.IsSuccessStatusCode) return null;
            var grupo = await resposta.Content.ReadFromJsonAsync<GrupoPessoasResposta>(cancellationToken: ct);
            if (string.IsNullOrWhiteSpace(grupo?.RecognitionModel)) return null;
            ModelosPorGrupo[personGroupId] = grupo.RecognitionModel;
            return grupo.RecognitionModel;
        }
        catch
        {
            return null;
        }
    }

    private class GrupoPessoasResposta
    {
        [JsonPropertyName("recognitionModel")]
        public string? RecognitionModel { get; set; }
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

    private record CandidatoIdentificacao(string PersonId, double Confidence, string? RequisicaoId = null);
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
