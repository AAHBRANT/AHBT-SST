using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Câmera de monitoramento de EPI cadastrada por obra. Na fase 1 a imagem vem da câmera do
// tablet (tela Monitorar); a câmera fixa da obra entra depois como outra fonte, sem mudar o cadastro.
public class Camera : AuditableEntity
{
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
    public ConfiancaAnaliseEpi Confianca { get; set; }

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
}
