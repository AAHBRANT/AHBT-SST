using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Pop-up de "novidades da versão" (requisito do usuário, 18/09): mostra uma vez por usuário, ao
// logar, o que mudou desde a última vez que ele acessou o sistema. Cadastro manual (tela em
// Administração), sem vínculo automático com o pipeline de deploy — o hash de commit não é
// legível pelo usuário final, então alguém (hoje: qualquer usuário logado) escreve o resumo em
// linguagem simples toda vez que publica algo que vale contar.
public class NovidadeVersao : AuditableEntity
{
    public string Titulo { get; set; } = string.Empty;
    public string Versao { get; set; } = string.Empty;
    public DateTime DataPublicacao { get; set; }

    public ICollection<NovidadeVersaoItem> Itens { get; set; } = new List<NovidadeVersaoItem>();
}
