namespace AAHBRANT.SST.Application.Obras;

// Regra compartilhada entre CriarObraCommandValidator (logo obrigatório no cadastro) e
// AnexarLogoObraCommandValidator (troca de logo depois) — mesma restrição de negócio, uma só
// fonte de verdade para não divergir entre os dois pontos de entrada.
public static class ValidacaoLogoObra
{
    public static readonly string[] TiposPermitidos = { "image/jpeg", "image/png" };
    public const int TamanhoMaximoBytes = 5 * 1024 * 1024;
}
