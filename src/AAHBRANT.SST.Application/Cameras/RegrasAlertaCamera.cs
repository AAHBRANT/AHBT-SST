using FluentValidation;

namespace AAHBRANT.SST.Application.Cameras;

// Limites e padrões das regras de alerta da câmera (confirmação por persistência, não repetir e modo sombra).
public static class RegrasAlertaCamera
{
    public const int PersistenciaMinimaSegundos = 1;
    public const int PersistenciaMaximaSegundos = 30;
    public const int PersistenciaPadraoSegundos = 3;

    public const int NaoRepetirMinimoSegundos = 0;
    public const int NaoRepetirMaximoSegundos = 3600;
    public const int NaoRepetirPadraoSegundos = 300;

    public static IRuleBuilderOptions<T, int> PersistenciaDeConfirmacao<T>(this IRuleBuilder<T, int> regra)
        => regra.InclusiveBetween(PersistenciaMinimaSegundos, PersistenciaMaximaSegundos)
            .WithMessage($"O tempo para confirmar precisa estar entre {PersistenciaMinimaSegundos} e {PersistenciaMaximaSegundos} segundos.");

    public static IRuleBuilderOptions<T, int> NaoRepetirAlerta<T>(this IRuleBuilder<T, int> regra)
        => regra.InclusiveBetween(NaoRepetirMinimoSegundos, NaoRepetirMaximoSegundos)
            .WithMessage($"O tempo para não repetir o alerta precisa estar entre {NaoRepetirMinimoSegundos} e {NaoRepetirMaximoSegundos} segundos (0 desliga).");
}
