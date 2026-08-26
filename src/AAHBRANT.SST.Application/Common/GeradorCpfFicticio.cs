namespace AAHBRANT.SST.Application.Common;

// Gera CPFs fictícios (nunca de pessoa real) com dígito verificador matematicamente válido,
// para popular dados de teste/desenvolvimento sem quebrar CpfValidador.EhValido em nenhuma tela.
// Faixa reservada 900000000-999999999 nos 9 primeiros dígitos — fora da faixa historicamente
// emitida pela Receita Federal, para deixar claro que não é um CPF real ainda que válido.
public static class GeradorCpfFicticio
{
    public static string Gerar(int indiceSequencial)
    {
        var baseNumerica = 900_000_000 + (indiceSequencial % 99_999_999);
        var noveDigitos = baseNumerica.ToString("D9");
        var digitos = noveDigitos.Select(c => c - '0').ToArray();

        var primeiroDigito = CalcularDigitoVerificador(digitos, 9);
        var digitosComPrimeiro = digitos.Append(primeiroDigito).ToArray();
        var segundoDigito = CalcularDigitoVerificador(digitosComPrimeiro, 10);

        return noveDigitos + primeiroDigito + segundoDigito;
    }

    private static int CalcularDigitoVerificador(int[] digitos, int quantidade)
    {
        var soma = 0;
        var multiplicador = quantidade + 1;
        for (var i = 0; i < quantidade; i++)
            soma += digitos[i] * multiplicador--;

        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
