namespace AAHBRANT.SST.Infrastructure.Integracao.Teams;

// Como chamar um usuário no Microsoft Graph. O AzureAdObjectId só é gravado depois do primeiro login do usuário
// no app pelo Teams; quem ainda não entrou ficava sem o sininho (o envio era recusado). O Graph aceita o
// e-mail (UPN) no lugar do id no caminho /users/{id|upn}/..., então usa o id quando existe e, se não, o e-mail.
public static class IdentificadorUsuarioGraph
{
    private static readonly char[] Proibidos = { '/', '\\', '?', '#', ' ', '%' };

    public static string? Obter(string? azureAdObjectId, string? email)
    {
        if (!string.IsNullOrWhiteSpace(azureAdObjectId)) return azureAdObjectId.Trim();

        var upn = email?.Trim();
        // E-mail só é aceito se parecer um UPN simples; nada que mude o caminho da requisição.
        return !string.IsNullOrEmpty(upn) && upn.Contains('@') && upn.IndexOfAny(Proibidos) < 0 ? upn : null;
    }
}
