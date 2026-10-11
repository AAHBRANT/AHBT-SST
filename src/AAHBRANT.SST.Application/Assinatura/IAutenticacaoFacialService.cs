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
}

public record ResultadoIdentificacaoFacial(bool Aceito, ResultadoAutenticacaoAssinatura? Resultado, MotivoRejeicaoFacial? Motivo, double? Confianca,
    // Quem o Azure apontou, quando a confiança permite atribuir a falha a alguém (cadastro fraco).
    Guid? TrabalhadorIdProvavel = null);

// Funcionários cuja foto o Azure não aceitou na migração: o cadastro deles foi arquivado e precisa ser
// refeito (o cadastro único volta a valer depois da nova captura).
public record ResultadoMigracaoModeloFacial(int Migrados, IReadOnlyList<string> SemCadastroAposMigracao);

public interface IAutenticacaoFacialService
{
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

    // Identifica quem está na foto dentro do PersonGroup da obra informada. Não recebe TrabalhadorId
    // — ao contrário do Futronic (que já resolveu o match localmente), aqui é o Azure quem descobre
    // quem é, a partir da foto.
    //
    // exigirMargemSobreSegundoColocado: usar na fila (ninguém escolhe a pessoa). Recusa quando o
    // segundo candidato está perto demais do primeiro (ver AssinaturaOptions.MargemMinima...).
    // Toda falha (confiança baixa, não reconhecido, ambíguo) é registrada para a lista de cadastros fracos.
    Task<ResultadoIdentificacaoFacial> IdentificarAsync(Guid obraId, byte[] fotoJpeg, CancellationToken ct, bool exigirMargemSobreSegundoColocado = false);
}
