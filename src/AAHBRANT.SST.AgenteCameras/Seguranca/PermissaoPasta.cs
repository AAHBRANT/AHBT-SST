using System.Security.AccessControl;
using System.Security.Principal;

namespace AAHBRANT.SST.AgenteCameras.Seguranca;

// A pasta de dados guarda a fila de imagens e a configuração. Se o Windows deixa usuários comuns (Users,
// Authenticated Users, Everyone) lerem ou gravarem nela, o serviço avisa no log da partida, com o comando que corrige.
// O serviço NÃO altera permissões sozinho (isso é decisão de quem administra a máquina).
public static class PermissaoPasta
{
    private static readonly (SecurityIdentifier Sid, string Nome)[] Amplos =
    {
        (new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), "Usuários"),
        (new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), "Usuários autenticados"),
        (new SecurityIdentifier(WellKnownSidType.WorldSid, null), "Todos"),
    };

    private const FileSystemRights Relevantes =
        FileSystemRights.ReadData | FileSystemRights.WriteData | FileSystemRights.AppendData |
        FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles |
        FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;

    public static string ComandoParaRestringir(string pasta) =>
        $"icacls \"{pasta}\" /inheritance:r /grant:r *S-1-5-18:(OI)(CI)F *S-1-5-32-544:(OI)(CI)F *S-1-5-19:(OI)(CI)M";

    /// <summary>Mensagem de aviso se a pasta estiver aberta a usuários comuns; null se estiver restrita ou não der para saber.</summary>
    public static string? AvaliarExposicao(string pasta)
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            var acl = new DirectoryInfo(pasta).GetAccessControl(AccessControlSections.Access);
            var quem = new List<string>();
            foreach (FileSystemAccessRule regra in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            {
                if (regra.AccessControlType != AccessControlType.Allow) continue;
                // InheritOnly: vale só para o que está dentro, não para a própria pasta.
                if (regra.PropagationFlags.HasFlag(PropagationFlags.InheritOnly)) continue;
                if ((regra.FileSystemRights & Relevantes) == 0) continue;
                foreach (var (sid, nome) in Amplos)
                    if (regra.IdentityReference.Equals(sid) && !quem.Contains(nome)) quem.Add(nome);
            }
            if (quem.Count == 0) return null;
            return $"A pasta de dados \"{pasta}\" (fila de imagens e configuração) permite acesso a usuários comuns do Windows ({string.Join(", ", quem)}). " +
                   $"Restrinja o acesso a SYSTEM, Administradores e à conta do serviço executando como administrador: {ComandoParaRestringir(pasta)}";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or PlatformNotSupportedException)
        {
            return null;
        }
    }
}
