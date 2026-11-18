using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Câmera de monitoramento de EPI cadastrada por obra. Na fase 1 a imagem vem da câmera do
// tablet (tela Monitorar); a câmera fixa da obra entra depois como outra fonte, sem mudar o cadastro.
public class Camera : AuditableEntity
{
    // Última vez que a cena SEM irregularidade gastou uma identificação facial só para conferir a cor do capacete.
    // Fica no banco (a API tem várias réplicas) para limitar essas identificações por câmera.
    public DateTime? UltimaIdentificacaoSoCorEmUtc { get; set; }

    public string Nome { get; set; } = string.Empty;

    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public Guid? AreaSstId { get; set; }
    public AreaSst? AreaSst { get; set; }

    public TipoCamera Tipo { get; set; } = TipoCamera.Canteiro;

    // Desligar o monitoramento sem excluir o cadastro (Ativo é a exclusão lógica).
    public bool Habilitada { get; set; } = true;

    // Vale para ocorrência sem trabalhador identificado (o identificado segue a regra de um alerta por
    // pessoa por hora). 0 = sem limite: alerta a cada nova ocorrência. Câmera nova nasce com 30 minutos.
    public int IntervaloMinimoAlertasMinutos { get; set; } = 30;

    // Origem da imagem. Navegador = tela Monitorar (tablet); Rtsp = câmera IP lida pelo agente instalado na obra, que usa
    // o equipamento (endereço e credencial), o canal (lente 1 ou 2) e o stream (0 = principal, 1 = secundário).
    // Câmera nova e as já existentes nascem Navegador, sem mudar nada do que funciona hoje.
    public FonteCamera Fonte { get; set; } = FonteCamera.Navegador;
    public Guid? EquipamentoCameraId { get; set; }
    public EquipamentoCamera? EquipamentoCamera { get; set; }
    public int? Canal { get; set; }
    public int? Stream { get; set; }

    // Intervalo mínimo entre quadros enviados pelo agente (segundos).
    public int IntervaloEnvioSegundos { get; set; } = 3;

    // Câmera IP de duas lentes empilhadas: qual metade da imagem esta câmera analisa. Só vale para Fonte = Rtsp; as demais ficam Inteira.
    public ParteImagemCamera ParteImagem { get; set; } = ParteImagemCamera.Inteira;

    // false = a análise e a ocorrência seguem iguais; só o aviso no Telegram é pulado.
    public bool AvisarTelegram { get; set; } = true;

    // Regras de alerta (Fase 2). A falta de EPI só vira ocorrência confirmada, e só então pode ir ao Telegram, quando a
    // MESMA irregularidade é vista de novo depois deste tempo (por item; 1 a 30 s na tela). Padrão 3 s.
    public int PersistenciaCapaceteSegundos { get; set; } = 3;
    public int PersistenciaColeteSegundos { get; set; } = 3;
    public int PersistenciaOculosSegundos { get; set; } = 3;

    // A mesma pessoa (ou a mesma posição, sem identificação) não gera novo alerta por este tempo (0 a 3600 s; 0 = só valem
    // as regras de agrupamento antigas). Padrão 300 s.
    public int NaoRepetirSegundos { get; set; } = 300;

    // true = analisa e grava as ocorrências (Sombra = true), mas NUNCA envia Telegram. AvisarTelegram é preservado e volta a
    // valer ao desligar. Câmera nova nasce true (CriarCameraCommand); as já existentes ficam false (padrão da migration).
    public bool ModoSombra { get; set; }

    // Atualizados quando o agente envia um quadro. A situação mostrada na tela é calculada na consulta.
    public SituacaoConexaoCamera SituacaoConexao { get; set; } = SituacaoConexaoCamera.Desconhecida;
    public DateTime? UltimoQuadroRecebidoEmUtc { get; set; }
    public string? MensagemConexao { get; set; }

    // Controle do teto de análises no servidor e do intervalo entre alertas.
    public DateTime? UltimaAnaliseEmUtc { get; set; }
    public DateTime? UltimoAlertaEmUtc { get; set; }

    // Quadros efetivamente enviados à IA (base do custo por foto); "ignorado" não conta.
    public int TotalAnalises { get; set; }

    // Consumo real da IA, somado a cada análise com os tokens que a própria API informa. O custo sai
    // destes números (ver CustoIaCameras), não de estimativa.
    public long TotalTokensEntrada { get; set; }
    public long TotalTokensSaida { get; set; }

    // Mesmo consumo, só do dia corrente (data em Brasília); zera na primeira análise do dia seguinte.
    public DateTime? DiaDoConsumo { get; set; }
    public int AnalisesNoDia { get; set; }
    public long TokensEntradaNoDia { get; set; }
    public long TokensSaidaNoDia { get; set; }

    // Análises do dia (mesma data em Brasília de DiaDoConsumo) em que só havia itens não conferidos (parte do corpo fora da
    // imagem) e nenhuma irregularidade: indica câmera mal posicionada. Zera junto com os demais contadores do dia.
    public int NaoAvaliaveisNoDia { get; set; }
}

// Pessoa sem capacete e/ou sem colete detectada numa foto. Só existe quando há irregularidade:
// fotos de cena regular não são gravadas.
public class OcorrenciaEpiCamera : AuditableEntity
{
    public Guid CameraId { get; set; }
    public Camera? Camera { get; set; }

    // Copiado da câmera para o filtro de escopo por obra não depender de navegação.
    public Guid ObraId { get; set; }

    public DateTime CapturadaEmUtc { get; set; }
    public DateTime RecebidaEmUtc { get; set; }

    public byte[] Foto { get; set; } = Array.Empty<byte>();

    public int PessoasNaCena { get; set; }
    public int SemCapacete { get; set; }
    public int SemColete { get; set; }
    // Pessoas em que se viu o rosto e não havia óculos de proteção (itens não avaliáveis não entram).
    public int SemOculos { get; set; }
    // Pessoas cujo item não foi julgado (parte do corpo fora da imagem, de costas, longe). Informativo: nunca motivam ocorrência.
    public int CapaceteNaoAvaliavel { get; set; }
    public int ColeteNaoAvaliavel { get; set; }
    public int OculosNaoAvaliavel { get; set; }
    public ConfiancaAnaliseEpi Confianca { get; set; }

    // Cor do capacete vista pela câmera (nulo = não distinguível ou "outra") e a esperada para a função do
    // trabalhador identificado (nulo = ninguém identificado ou função sem cor).
    public CorCapacete? CorCapaceteVista { get; set; }
    public CorCapacete? CorCapaceteEsperada { get; set; }
    // true = ocorrência só de cor do capacete diferente da função (gravidade menor, sem Telegram).
    public bool AvisoCorCapacete { get; set; }

    public bool AlertaEnviado { get; set; }
    public DateTime? AlertaEnviadoEmUtc { get; set; }

    // Trabalhador da ocorrência: vinculado à mão pelo técnico ou identificado pelo rosto (galeria facial
    // da obra) quando há uma única pessoa na cena.
    public Guid? TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }
    public Guid? VinculadoPorUsuarioId { get; set; }
    public DateTime? VinculadoEmUtc { get; set; }

    // true = o vínculo veio do reconhecimento facial, sem conferência humana. Volta a false quando o
    // técnico troca ou remove o trabalhador.
    public bool IdentificadoPorRosto { get; set; }
    // Confiança devolvida pelo reconhecimento facial (0 a 100), só quando IdentificadoPorRosto.
    public double? ConfiancaIdentificacao { get; set; }

    public SituacaoOcorrenciaEpi Situacao { get; set; } = SituacaoOcorrenciaEpi.Nova;

    // A análise roda sem parar: a mesma pessoa parada sem EPI seria uma ocorrência nova a cada foto. As
    // repetições são somadas na ocorrência já aberta (a foto e o horário continuam os da primeira vez).
    public int VezesVista { get; set; } = 1;
    public DateTime? UltimaVezEmUtc { get; set; }

    // Confirmação (Fase 2): a primeira vista de uma irregularidade nasce Confirmada = false e sem Telegram; ela confirma quando a
    // mesma irregularidade é vista de novo depois do tempo mínimo do item (ver AnalisarQuadroCameraCommandHandler). O padrão
    // do CLR é true de propósito: as ocorrências anteriores à Fase 2 (migration) e as criadas fora do analisador valem como
    // confirmadas; o analisador grava false explicitamente.
    public bool Confirmada { get; set; } = true;
    public DateTime? ConfirmadaEmUtc { get; set; }
    // Segundos entre a primeira e a última vez em que a irregularidade foi vista (nulo = ocorrência anterior à Fase 2).
    public int? SegundosObservados { get; set; }

    // true = gravada com a câmera em modo sombra: registro para conferência, nunca foi (nem será) enviada ao Telegram.
    public bool Sombra { get; set; }

    // Posição aproximada da cabeça na última vista (fração da imagem, 0 a 1). Guardada no banco, e não em memória, porque a API
    // tem várias réplicas: é com ela que a mesma pessoa sem identificação é reconhecida de um quadro para o outro.
    public double? PosicaoX { get; set; }
    public double? PosicaoY { get; set; }
}
