namespace AAHBRANT.SST.Application.Common;

public static class CpfValidador
{
    public static bool EhValido(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf) || cpf.Length != 11 || !cpf.All(char.IsDigit))
            return false;

        if (cpf.Distinct().Count() == 1)
            return false;

        var digitos = cpf.Select(c => c - '0').ToArray();

        var primeiroDigito = CalcularDigitoVerificador(digitos, 9);
        if (primeiroDigito != digitos[9])
            return false;

        var segundoDigito = CalcularDigitoVerificador(digitos, 10);
        return segundoDigito == digitos[10];
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
