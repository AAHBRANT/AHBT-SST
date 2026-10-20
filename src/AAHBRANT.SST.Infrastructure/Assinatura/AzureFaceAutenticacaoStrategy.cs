using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common;
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

    // Modelo de reconhecimento dos grupos de pessoas. O padrão do Azure (quando não se informa nada) é o
    // recognition_01, o mais antigo e menos preciso: com ele, quem é a pessoa certa ficava com 70% a 85%
    // de confiança e era recusado (dados reais de 08/10, Consórcio Ponte Rio Cuiá). O modelo de um grupo
    // não muda depois de criado; grupos antigos são recriados por MigrarModeloReconhecimentoAsync.
    public const string ModeloReconhecimentoAtual = "recognition_04";
    // Modelo de detecção usado nas fotos de assinatura e nas faces cadastradas: acha melhor rostos
    // pequenos, de lado ou com máscara/capacete do que o detection_01 (padrão).
    private const string ModeloDeteccao = "detection_03";

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

        // Grupo e Person gravados antes do treino: se o treino falhar, a próxima tentativa reaproveita o
        // mesmo Person em vez de deixar um órfão no grupo. A foto só é gravada depois do treino concluído,
        // porque existir foto = cadastro pronto (auditoria 19/10, A09: treino falho travava o recadastro).
        await _db.SaveChangesAsync(ct);
        await TreinarEAguardarAsync(cliente, personGroupId, ct);

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
    }

    public async Task RemoverCadastroAsync(Guid trabalhadorId, CancellationToken ct)
    {
        // IgnoreQueryFilters: também é chamado no desligamento, quando o trabalhador já está inativo.
        var trabalhador = await _db.Trabalhadores.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == trabalhadorId, ct)
            ?? throw new KeyNotFoundException("Trabalhador não encontrado.");
        if (trabalhador.AzureFacePersonId is null)
            return;

        var obra = await _db.Obras.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == trabalhador.ObraId, ct)
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

    public async Task RealocarCadastroAsync(Guid trabalhadorId, Guid obraAnteriorId, CancellationToken ct)
    {
        // IgnoreQueryFilters: a troca de obra também chega pela integração com o G-RH, sem usuário com
        // acesso às duas obras. As fotos arquivadas (Ativo = false) ficam de fora pelo filtro explícito.
        var trabalhador = await _db.Trabalhadores.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == trabalhadorId, ct);
        if (trabalhador?.AzureFacePersonId is null || trabalhador.ObraId == obraAnteriorId)
            return;

        var foto = await _db.FotosCadastroFacial.IgnoreQueryFilters()
            .Where(f => f.TrabalhadorId == trabalhadorId && f.Ativo)
            .OrderByDescending(f => f.CapturadaEm)
            .FirstOrDefaultAsync(ct);
        if (foto is null)
            return;

        var obraNova = await _db.Obras.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == trabalhador.ObraId, ct)
            ?? throw new KeyNotFoundException("Obra do trabalhador não encontrada.");
        var grupoAnterior = await _db.Obras.IgnoreQueryFilters()
            .Where(o => o.Id == obraAnteriorId)
            .Select(o => o.AzureFacePersonGroupId)
            .FirstOrDefaultAsync(ct);

        using var cliente = CriarCliente();

        // A foto já passou pela validação de qualidade no cadastro original: aqui só é copiada.
        var grupoNovo = obraNova.AzureFacePersonGroupId;
        if (grupoNovo is null)
        {
            grupoNovo = $"obra-{obraNova.Id:N}";
            obraNova.AzureFacePersonGroupId = grupoNovo;
        }
        await CriarPersonGroupSeNaoExistirAsync(cliente, grupoNovo, obraNova.Nome, ct);

        var personAnterior = trabalhador.AzureFacePersonId;
        var personNovo = await CriarPersonAsync(cliente, grupoNovo, trabalhador.Nome, ct);
        var erro = await TentarAdicionarFaceAsync(cliente, grupoNovo, personNovo, foto.Conteudo, ct);
        if (erro is not null)
            throw new InvalidOperationException(MontarMensagemErroAzure("Falha ao levar o cadastro facial para a nova obra", erro));

        trabalhador.AzureFacePersonId = personNovo;
        await _db.SaveChangesAsync(ct);
        await TreinarEAguardarAsync(cliente, grupoNovo, ct);

        // Limpeza do grupo antigo é melhor esforço: o id antigo não aponta mais para ninguém no banco, então
        // mesmo que fique no Azure a identificação na obra anterior já não o aceita.
        if (grupoAnterior is null)
            return;
        try
        {
            var resposta = await cliente.DeleteAsync($"face/v1.0/persongroups/{grupoAnterior}/persons/{personAnterior}", ct);
            if (resposta.IsSuccessStatusCode)
                await TreinarEAguardarAsync(cliente, grupoAnterior, ct);
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException)
        {
        }
    }

    public async Task<ResultadoMigracaoModeloFacial?> MigrarModeloReconhecimentoAsync(Guid obraId, CancellationToken ct)
    {
        var obra = await _db.Obras.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == obraId, ct)
            ?? throw new KeyNotFoundException("Obra não encontrada.");
        var grupoAntigo = obra.AzureFacePersonGroupId;
        if (grupoAntigo is null)
            return null;

        using var cliente = CriarCliente();

        // Lido direto do Azure (sem o cache): é o que decide se a obra ainda precisa migrar.
        var respostaGrupo = await cliente.GetAsync($"face/v1.0/persongroups/{grupoAntigo}", ct);
        if (respostaGrupo.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        if (!respostaGrupo.IsSuccessStatusCode)
            throw new InvalidOperationException($"Falha ao consultar o grupo {grupoAntigo} no Azure Face API: {respostaGrupo.StatusCode}");
        var grupo = await respostaGrupo.Content.ReadFromJsonAsync<GrupoPessoasResposta>(cancellationToken: ct);
        if (string.Equals(grupo?.RecognitionModel, ModeloReconhecimentoAtual, StringComparison.OrdinalIgnoreCase))
        {
            ModelosPorGrupo[grupoAntigo] = ModeloReconhecimentoAtual;
            return null;
        }

        // Id novo e fixo por obra. Se uma migração anterior parou no meio, o grupo novo é apagado e refeito
        // do zero, para não ficar com pessoas duplicadas.
        var grupoNovo = $"obra-{obra.Id:N}-r4";
        await EnviarComRetryMigracaoAsync(() => cliente.DeleteAsync($"face/v1.0/persongroups/{grupoNovo}", ct), ct);
        await CriarPersonGroupSeNaoExistirAsync(cliente, grupoNovo, obra.Nome, ct);

        var novosIds = new Dictionary<Guid, string>();
        var falharam = new List<Guid>();

        // Duas passadas: a segunda pega quem fez cadastro (ou recadastro) enquanto a primeira rodava.
        for (var passada = 0; passada < 2; passada++)
        {
            var jaTratados = novosIds.Keys.Concat(falharam).ToList();
            var pendentes = await _db.Trabalhadores.IgnoreQueryFilters()
                .Where(t => t.ObraId == obra.Id && t.AzureFacePersonId != null && !jaTratados.Contains(t.Id))
                .Select(t => new { t.Id, t.Nome })
                .ToListAsync(ct);
            foreach (var t in pendentes)
            {
                var foto = await _db.FotosCadastroFacial.IgnoreQueryFilters()
                    .Where(f => f.TrabalhadorId == t.Id && f.Ativo)
                    .OrderByDescending(f => f.CapturadaEm)
                    .Select(f => f.Conteudo)
                    .FirstOrDefaultAsync(ct);
                if (foto is null)
                {
                    falharam.Add(t.Id);
                    continue;
                }

                var respostaPessoa = await EnviarComRetryMigracaoAsync(
                    () => cliente.PostAsJsonAsync($"face/v1.0/persongroups/{grupoNovo}/persons", new { name = t.Nome }, ct), ct);
                if (!respostaPessoa.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Falha ao criar Person na migração do grupo {grupoNovo}: {respostaPessoa.StatusCode}");
                var personId = (await respostaPessoa.Content.ReadFromJsonAsync<PersonCriadoResposta>(cancellationToken: ct))!.PersonId;

                var respostaFace = await EnviarComRetryMigracaoAsync(async () =>
                {
                    using var conteudo = new ByteArrayContent(foto);
                    conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    return await cliente.PostAsync(
                        $"face/v1.0/persongroups/{grupoNovo}/persons/{personId}/persistedFaces?detectionModel={ModeloDeteccao}", conteudo, ct);
                }, ct);
                if (respostaFace.IsSuccessStatusCode)
                    novosIds[t.Id] = personId;
                else if ((int)respostaFace.StatusCode == 400)
                    falharam.Add(t.Id); // foto recusada pelo Azure (ex.: rosto não detectado): cadastro precisa ser refeito
                else
                    throw new InvalidOperationException($"Falha ao adicionar face na migração do grupo {grupoNovo}: {respostaFace.StatusCode}");

                if (_options.IntervaloMigracaoFacialMs > 0)
                    await Task.Delay(_options.IntervaloMigracaoFacialMs, ct);
            }
        }

        if (novosIds.Count > 0)
            await TreinarEAguardarAsync(cliente, grupoNovo, ct, tentativas: 300);

        // Troca tudo de uma vez: a obra passa a usar o grupo novo e cada trabalhador o id novo. Quem não pôde
        // ser migrado fica sem cadastro (fotos arquivadas, como no "Refazer cadastro facial") para poder ser
        // cadastrado de novo — senão ficaria apontando para um grupo que vai ser apagado.
        var tratados = novosIds.Keys.Concat(falharam).ToList();
        var trabalhadores = await _db.Trabalhadores.IgnoreQueryFilters()
            .Where(t => tratados.Contains(t.Id))
            .ToListAsync(ct);
        foreach (var t in trabalhadores)
            t.AzureFacePersonId = novosIds.TryGetValue(t.Id, out var id) ? id : null;
        if (falharam.Count > 0)
        {
            var fotos = await _db.FotosCadastroFacial.IgnoreQueryFilters()
                .Where(f => falharam.Contains(f.TrabalhadorId) && f.Ativo)
                .ToListAsync(ct);
            _db.FotosCadastroFacial.RemoveRange(fotos);
        }
        obra.AzureFacePersonGroupId = grupoNovo;
        await _db.SaveChangesAsync(ct);
        ModelosPorGrupo[grupoNovo] = ModeloReconhecimentoAtual;

        // O grupo antigo não é mais usado por ninguém; apagar é só limpeza (melhor esforço).
        try
        {
            await cliente.DeleteAsync($"face/v1.0/persongroups/{grupoAntigo}", ct);
        }
        catch (HttpRequestException)
        {
        }

        return new ResultadoMigracaoModeloFacial(
            novosIds.Count,
            trabalhadores.Where(t => falharam.Contains(t.Id)).Select(t => t.Nome).ToList());
    }

    // A migração faz muitas chamadas seguidas; no tier F0 (20/min) o 429 é esperado. Espera o tempo que o
    // Azure pedir (Retry-After) e tenta de novo, por bem mais tempo que o retry curto da assinatura.
    private static async Task<HttpResponseMessage> EnviarComRetryMigracaoAsync(Func<Task<HttpResponseMessage>> enviar, CancellationToken ct)
    {
        for (var tentativa = 0; ; tentativa++)
        {
            var resposta = await enviar();
            if (resposta.StatusCode != (System.Net.HttpStatusCode)429 || tentativa >= 20)
                return resposta;
            var espera = resposta.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(10);
            resposta.Dispose();
            await Task.Delay(espera, ct);
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

    public async Task<ResultadoIdentificacaoFacial> IdentificarAsync(Guid obraId, byte[] fotoJpeg, CancellationToken ct, bool exigirMargemSobreSegundoColocado = true)
    {
        var obra = await _db.Obras.FirstOrDefaultAsync(o => o.Id == obraId, ct)
            ?? throw new KeyNotFoundException("Obra não encontrada.");

        if (!obra.MetodosAutenticacaoHabilitados.HasFlag(MetodoAutenticacaoObra.ReconhecimentoFacial))
            throw new InvalidOperationException("Este método de assinatura não está habilitado para a obra deste trabalhador.");

        if (obra.AzureFacePersonGroupId is null)
            return new ResultadoIdentificacaoFacial(false, null, MotivoRejeicaoFacial.RostoNaoReconhecido, null);

        using var cliente = CriarCliente();

        // O faceId precisa ser gerado com o mesmo modelo do grupo, senão o identify recusa. Enquanto um grupo
        // antigo (recognition_01) não é migrado, a foto é lida com o modelo dele.
        var modelo = await ObterModeloReconhecimentoDoGrupoAsync(cliente, obra.AzureFacePersonGroupId, ct);
        var faceIds = await DetectarRostosAsync(cliente, fotoJpeg, modelo, ct);
        if (faceIds.Count == 0)
            return new ResultadoIdentificacaoFacial(false, null, MotivoRejeicaoFacial.NenhumRostoDetectado, null);
        if (faceIds.Count > 1)
            return new ResultadoIdentificacaoFacial(false, null, MotivoRejeicaoFacial.MultiplosRostosDetectados, null);

        CandidatoIdentificacao? candidato;
        double? confiancaSegundo;
        try
        {
            (candidato, confiancaSegundo) = await IdentificarRostoAsync(cliente, obra.AzureFacePersonGroupId, faceIds[0], ct);
        }
        catch (ModeloReconhecimentoIncompativelException)
        {
            // O modelo guardado (ou o padrão, quando a consulta do grupo falhou) não bate com o do grupo —
            // ex.: grupo migrado para recognition_04 ou consulta que falhou e caiu no recognition_01.
            // Relê o modelo direto do Azure e refaz detect + identify uma vez.
            ModelosPorGrupo.TryRemove(obra.AzureFacePersonGroupId, out _);
            modelo = await ObterModeloReconhecimentoDoGrupoAsync(cliente, obra.AzureFacePersonGroupId, ct) ?? ModeloReconhecimentoAtual;
            faceIds = await DetectarRostosAsync(cliente, fotoJpeg, modelo, ct);
            if (faceIds.Count != 1)
                return new ResultadoIdentificacaoFacial(false, null,
                    faceIds.Count == 0 ? MotivoRejeicaoFacial.NenhumRostoDetectado : MotivoRejeicaoFacial.MultiplosRostosDetectados, null);
            try
            {
                (candidato, confiancaSegundo) = await IdentificarRostoAsync(cliente, obra.AzureFacePersonGroupId, faceIds[0], ct);
            }
            catch (ModeloReconhecimentoIncompativelException)
            {
                throw ErroDeIndisponibilidade(System.Net.HttpStatusCode.BadRequest,
                    $"identify: recognitionModel incompatível mesmo após reler o modelo do grupo {obra.AzureFacePersonGroupId} ({modelo})");
            }
        }
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

        // Consentimento LGPD conferido também na hora de assinar (antes só no cadastro): quem revogou o
        // consentimento não pode continuar sendo identificado pelo rosto.
        if (trabalhador.TermoAceiteAssinaturaEletronicaEm is null || trabalhador.ConsentimentoBiometriaEm is null)
            throw new InvalidOperationException($"{trabalhador.Nome} não tem o Termo de Aceite e o consentimento de biometria ativos. Use outro método de assinatura.");

        // Fila 1:N: ninguém escolhe a pessoa, então só confirma se o primeiro colocado estiver bem à
        // frente do segundo. Dois candidatos próximos = provável colega parecido, melhor usar a digital.
        if (exigirMargemSobreSegundoColocado && confiancaSegundo is { } segundo
            && candidato.Confidence - segundo < _options.MargemMinimaIdentificacaoFacial)
            return await RejeitarAsync(obraId, MotivoRejeicaoFacial.RostoAmbiguo, candidato.Confidence, null, ct);

        // Rastro da validação para o log de assinaturas: modelo real do grupo (lido do Azure), grupo e
        // id da requisição. Nunca bloqueia a assinatura: se não vier, o log mostra "não informado".
        var resultado = new ResultadoAutenticacaoAssinatura(
            trabalhador.Id, MetodoAutenticacaoAssinatura.ReconhecimentoFacial,
            ValidacaoModelo: modelo, ValidacaoGrupoId: obra.AzureFacePersonGroupId, ValidacaoRequisicaoId: candidato.RequisicaoId,
            Confianca: candidato.Confidence * 100);
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
        var resposta = await cliente.PutAsJsonAsync($"face/v1.0/persongroups/{personGroupId}", new { name = nomeObra, recognitionModel = ModeloReconhecimentoAtual }, ct);
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
        var resposta = await cliente.PostAsync($"face/v1.0/persongroups/{personGroupId}/persons/{personId}/persistedFaces?detectionModel={ModeloDeteccao}", conteudo, ct);
        if (resposta.IsSuccessStatusCode)
            return null;
        return await LerErroAzureAsync(resposta, ct);
    }

    private static async Task TreinarEAguardarAsync(HttpClient cliente, string personGroupId, CancellationToken ct, int tentativas = 10)
    {
        var respostaTreino = await cliente.PostAsync($"face/v1.0/persongroups/{personGroupId}/train", content: null, ct);
        if (!respostaTreino.IsSuccessStatusCode)
            throw new InvalidOperationException($"Falha ao disparar treino no Azure Face API: {respostaTreino.StatusCode}");

        // Treino é assíncrono no Azure — poll com backoff curto (ação administrativa pontual, ok
        // bloquear por alguns segundos). 10 tentativas de 1s cobre o caso comum (grupo pequeno).
        for (var tentativa = 0; tentativa < tentativas; tentativa++)
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

    private static async Task<List<string>> DetectarRostosAsync(HttpClient cliente, byte[] fotoJpeg, string? modeloGrupo, CancellationToken ct)
    {
        // Sem o modelo do grupo (consulta falhou) segue o padrão do Azure, que é o comportamento antigo.
        var url = modeloGrupo is null
            ? $"face/v1.0/detect?returnFaceId=true&detectionModel={ModeloDeteccao}"
            : $"face/v1.0/detect?returnFaceId=true&detectionModel={ModeloDeteccao}&recognitionModel={modeloGrupo}";
        var resposta = await EnviarComRetry429Async(async () =>
        {
            using var conteudo = new ByteArrayContent(fotoJpeg);
            conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return await cliente.PostAsync(url, conteudo, ct);
        }, ct);
        if (!resposta.IsSuccessStatusCode)
            throw await ErroDeIndisponibilidadeAsync(resposta, "detect", ct);
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
            if (erro.Message.Contains("recognitionModel", StringComparison.OrdinalIgnoreCase))
                throw new ModeloReconhecimentoIncompativelException();
            throw ErroDeIndisponibilidade(resposta.StatusCode, MontarMensagemErroAzure("identify", erro));
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

    // Erro do Azure na hora de assinar (cota estourada, serviço fora): a pessoa vê o que fazer, não o código
    // técnico ("TooManyRequests"), que antes aparecia cru na tela. O detalhe vai junto na exceção interna,
    // para o log do servidor.
    private static async Task<ServicoIndisponivelException> ErroDeIndisponibilidadeAsync(HttpResponseMessage resposta, string chamada, CancellationToken ct)
        => ErroDeIndisponibilidade(resposta.StatusCode, MontarMensagemErroAzure(chamada, await LerErroAzureAsync(resposta, ct)));

    private static ServicoIndisponivelException ErroDeIndisponibilidade(System.Net.HttpStatusCode status, string detalhe)
    {
        var mensagem = (int)status == 429
            ? "O reconhecimento facial está com muitas assinaturas ao mesmo tempo. Aguarde 30 segundos e tente de novo, ou use a digital."
            : "O reconhecimento facial está indisponível no momento. Tente de novo em instantes ou use a digital.";
        return new ServicoIndisponivelException(mensagem, new HttpRequestException($"Azure Face API: {detalhe}", null, status));
    }

    private static string MontarMensagemErroAzure(string contexto, AzureFaceErro erro)
        => $"{contexto}: {erro.Status} ({erro.Code}: {erro.Message})";

    private record CandidatoIdentificacao(string PersonId, double Confidence, string? RequisicaoId = null);
    private record AzureFaceErro(string Status, string Code, string Message);
    private sealed class ModeloReconhecimentoIncompativelException : Exception;

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
