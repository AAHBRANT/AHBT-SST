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

    // 0 = sem limite: alerta a cada ocorrência detectada.
    public int IntervaloMinimoAlertasMinutos { get; set; }

    // Controle do teto de análises no servidor e do intervalo entre alertas.
    public DateTime? UltimaAnaliseEmUtc { get; set; }
    public DateTime? UltimoAlertaEmUtc { get; set; }

    // Quadros efetivamente enviados à IA (base do custo por foto); "ignorado" não conta.
    public int TotalAnalises { get; set; }
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

    // Vínculo manual feito pelo técnico (fase 1 não identifica o trabalhador automaticamente).
    public Guid? TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }
    public Guid? VinculadoPorUsuarioId { get; set; }
    public DateTime? VinculadoEmUtc { get; set; }

    public SituacaoOcorrenciaEpi Situacao { get; set; } = SituacaoOcorrenciaEpi.Nova;
}
