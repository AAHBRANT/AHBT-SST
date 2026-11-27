namespace AAHBRANT.SST.Application.Assinatura;

// Motivos de rejeição distintos para mensagens específicas na UI (docs/superpowers/specs/2026-09-04-
// assinatura-facial-azure-design.md §3) — "nenhum rosto" e "confiança baixa" merecem textos
// diferentes de "múltiplos rostos".
public enum MotivoRejeicaoFacial
{
    NenhumRostoDetectado,
    MultiplosRostosDetectados,
    ConfiancaBaixa,
    RostoNaoReconhecido,
    // Fila 1:N: o segundo colocado ficou perto demais do primeiro para confirmar com segurança.
    RostoAmbiguo,
    // O Azure reconheceu o rosto, mas nenhum funcionário da obra está ligado a essa pessoa (vínculo quebrado).
    CadastroSemVinculo,
}

public record ResultadoIdentificacaoFacial(bool Aceito, ResultadoAutenticacaoAssinatura? Resultado, MotivoRejeicaoFacial? Motivo, double? Confianca,
    // Quem o Azure apontou, quando a confiança permite atribuir a falha a alguém (cadastro fraco).
    Guid? TrabalhadorIdProvavel = null);

// Funcionários cuja foto o Azure não aceitou na migração: o cadastro deles foi arquivado e precisa ser
// refeito (o cadastro único volta a valer depois da nova captura).
public record ResultadoMigracaoModeloFacial(int Migrados, IReadOnlyList<string> SemCadastroAposMigracao);

// Conferência entre o banco e o grupo facial da obra no Azure (30/10, caso Cuiá). Só leitura.
// - VinculosQuebrados: funcionário com cadastro no banco apontando para uma pessoa que não existe no grupo
//   (nunca é reconhecido). Precisa refazer o cadastro facial.
// - PessoasSemVinculo: pessoa no grupo do Azure que nenhum funcionário da obra aponta (cadastro solto,
//   normalmente duplicado). Pode ser reconhecida no lugar do cadastro certo; não é apagada aqui.
// - DuplicadosNoBanco: dois ou mais funcionários apontando para a mesma pessoa do Azure.
public record VinculoFacialQuebradoDto(Guid TrabalhadorId, string Nome, string? Matricula, string PersonIdAzure, bool TemFotoCadastro);
public record PessoaFacialSemVinculoDto(string PersonIdAzure, string NomeNoAzure, int Faces);
public record DiagnosticoVinculosFacialDto(
    Guid ObraId,
    string ObraNome,
    string? GrupoAzure,
    bool GrupoExisteNoAzure,
    int CadastrosNoBanco,
    int PessoasNoAzure,
    IReadOnlyList<VinculoFacialQuebradoDto> VinculosQuebrados,
    IReadOnlyList<PessoaFacialSemVinculoDto> PessoasSemVinculo,
    IReadOnlyList<VinculoFacialQuebradoDto> DuplicadosNoBanco);

// Teste rápido do serviço de reconhecimento facial (uma consulta leve ao Azure, sem foto). Ok=false quando o
// Azure recusa ou não responde: é o sinal que a vigia do Worker usa para avisar antes de a obra abrir a fila
// (27/11: o Azure passou a recusar o endereço do recurso às 22h e a obra só descobriu às 6h58).
public record VerificacaoServicoFacialDto(bool Ok, string Endpoint, int? StatusHttp, string? CodigoErro, string? Mensagem, long DuracaoMs);

// Situação do treino do grupo de rostos de uma obra no Azure ("succeeded", "running", "failed" ou "notstarted").
public record TreinoGrupoFacialDto(string? Status, DateTime? UltimaAcaoEm, string? Mensagem);

public interface IAutenticacaoFacialService
{
    // Implementações de teste não precisam desses três: só a do Azure fala com o serviço de verdade.
    Task<VerificacaoServicoFacialDto> VerificarServicoAsync(CancellationToken ct)
        => Task.FromResult(new VerificacaoServicoFacialDto(true, "(não verificado)", null, null, null, 0));

    Task<TreinoGrupoFacialDto?> ObterTreinoGrupoAsync(Guid obraId, CancellationToken ct)
        => Task.FromResult<TreinoGrupoFacialDto?>(null);

    // Dispara um novo treino do grupo da obra e aguarda terminar. Seguro e reversível: não apaga nem cria
    // pessoa, só recalcula o índice do Azure com as faces que já estão lá.
    Task RetreinarGrupoAsync(Guid obraId, CancellationToken ct) => Task.CompletedTask;

    // IRREVERSÍVEL — só depois de aprovação do responsável (PropostaAcaoAssinatura). Exclui do grupo da obra
    // as pessoas pedidas que AINDA estão sem funcionário ligado (diagnóstico refeito na hora: quem ganhou
    // vínculo desde a proposta fica) e retreina o grupo. Devolve quantas saíram.
    Task<int> ExcluirPessoasSemVinculoAsync(Guid obraId, IReadOnlyCollection<string> personIds, CancellationToken ct)
        => Task.FromResult(0);

    // Compara os cadastros faciais do banco com o grupo da obra no Azure, sem alterar nada.
    Task<DiagnosticoVinculosFacialDto> DiagnosticarVinculosAsync(Guid obraId, CancellationToken ct);

    // Cadastra (ou atualiza) a face do trabalhador no Azure — cria o PersonGroup da obra se ainda não
    // existir, cria o Person se ainda não existir, adiciona a foto e dispara o treino, aguardando a
    // conclusão (síncrono — ação administrativa pontual, não precisa ser assíncrona).
    Task CadastrarAsync(Guid trabalhadorId, byte[] fotoJpeg, CancellationToken ct);

    // Apaga a pessoa (e todas as faces dela) do PersonGroup da obra no Azure e retreina o grupo. Usado
    // ao refazer o cadastro facial. Idempotente: se a pessoa já não existe no Azure, não falha — assim
    // dá para repetir a operação depois de uma falha parcial. Não mexe no banco; quem chama limpa
    // AzureFacePersonId e as fotos de cadastro.
    Task RemoverCadastroAsync(Guid trabalhadorId, CancellationToken ct);

    // Leva o cadastro facial para o PersonGroup da obra ATUAL do trabalhador, usando a foto de cadastro
    // já aprovada (sem nova captura). Chamar depois de salvar a troca de obra. O Azure procura o rosto só
    // no grupo da obra do documento, então sem isto quem mudava de obra nunca mais era reconhecido
    // ("Rosto não reconhecido") e o cadastro aparecia como já feito. Não faz nada se não houver cadastro
    // ou se a obra não mudou.
    Task RealocarCadastroAsync(Guid trabalhadorId, Guid obraAnteriorId, CancellationToken ct);

    // Recria o grupo de pessoas da obra no modelo de reconhecimento atual (recognition_04), reaproveitando
    // as fotos de cadastro já aprovadas — ninguém precisa ser fotografado de novo. Devolve null quando não
    // há nada a fazer (obra sem grupo ou grupo já no modelo atual). Pode levar minutos: roda em segundo
    // plano (MigracaoModeloFacialBackgroundService), nunca dentro de uma requisição.
    Task<ResultadoMigracaoModeloFacial?> MigrarModeloReconhecimentoAsync(Guid obraId, CancellationToken ct);

    // Reconstrução forçada pelo Administrador (endpoint "recriar grupo"): refaz o grupo das fotos guardadas
    // mesmo quando ele já está no modelo atual — limpa pessoas duplicadas ou soltas deixadas por falhas
    // antigas. O grupo em uso nunca é apagado antes de o novo estar pronto e treinado.
    Task<ResultadoMigracaoModeloFacial?> ReconstruirGrupoAsync(Guid obraId, CancellationToken ct)
        => MigrarModeloReconhecimentoAsync(obraId, ct);

    // Identifica quem está na foto dentro do PersonGroup da obra informada. Não recebe TrabalhadorId
    // — ao contrário do Futronic (que já resolveu o match localmente), aqui é o Azure quem descobre
    // quem é, a partir da foto.
    //
    // exigirMargemSobreSegundoColocado: recusa quando o segundo candidato está perto demais do primeiro
    // (ver AssinaturaOptions.MargemMinima...). Ligado por padrão em todo fluxo 1:N (auditoria 19/10, A10).
    // Toda falha (confiança baixa, não reconhecido, ambíguo) é registrada para a lista de cadastros fracos.
    Task<ResultadoIdentificacaoFacial> IdentificarAsync(Guid obraId, byte[] fotoJpeg, CancellationToken ct, bool exigirMargemSobreSegundoColocado = true);

    // Mesma identificação 1:N, mas sem gravar a falha na lista de cadastros fracos. Para as câmeras de EPI,
    // em que a foto vem de longe e sem pose: não reconhecer é o esperado, não sinal de cadastro ruim.
    Task<ResultadoIdentificacaoFacial> IdentificarSemRegistrarFalhaAsync(Guid obraId, byte[] fotoJpeg, CancellationToken ct)
        => IdentificarAsync(obraId, fotoJpeg, ct);

    // Câmeras de EPI com várias pessoas na cena: devolve todos os rostos encontrados na foto, cada um com a
    // posição e, quando reconhecido com segurança (mesmos limiares e margem da identificação 1:N), o
    // trabalhador. Não grava falha de cadastro fraco. Obra sem facial ou sem grupo: lista vazia.
    Task<IReadOnlyList<RostoNaCena>> IdentificarRostosDaCenaAsync(Guid obraId, byte[] fotoJpeg, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<RostoNaCena>>(Array.Empty<RostoNaCena>());
}

// Rosto encontrado numa foto de cena. Posição em fração da imagem (0 a 1, a partir do canto superior
// esquerdo). TrabalhadorId nulo = rosto visto, mas não reconhecido com segurança. Confiança de 0 a 100.
public record RostoNaCena(double X, double Y, double Largura, double Altura, Guid? TrabalhadorId, double? Confianca)
{
    public double CentroX => X + Largura / 2;
    public double CentroY => Y + Altura / 2;
}
