namespace AAHBRANT.SST.Application.Assinatura;

// Endereço de IP é dado pessoal. Na página pública de validação — anônima, alcançável por qualquer
// pessoa que fotografe o QR — ele aparece mascarado: prova que existe registro da origem de rede da
// assinatura, sem publicar o endereço completo de quem assinou. O valor íntegro continua em
// DocumentoSignatario.IpAddress, acessível só por quem tem "assinatura:ver".
//
// Mesma linha de raciocínio da decisão de não servir o PDF na rota pública (23/09).
public static class IpMascarador
{
    public static string? Mascarar(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return null;

        // IPv6: mantém só os dois primeiros grupos, que indicam a rede, nunca o host.
        if (ip.Contains(':'))
        {
            var grupos = ip.Split(':');
            return grupos.Length >= 2 ? $"{grupos[0]}:{grupos[1]}:…" : "…";
        }

        var octetos = ip.Split('.');
        if (octetos.Length != 4)
            return "…";

        return $"{octetos[0]}.{octetos[1]}.{octetos[2]}.xxx";
    }
}
