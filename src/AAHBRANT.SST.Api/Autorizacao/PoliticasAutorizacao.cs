namespace AAHBRANT.SST.Api.Autorizacao;

public static class PoliticasAutorizacao
{
    // Exclusão de registro operacional é privilégio de Administrador (pedido do usuário, 23/09):
    // "apenas o ADM pode apagar". Diferente de todas as outras policies deste projeto, este nome NÃO
    // é um Permissao.Codigo — é regra fixa, resolvida por PermissaoAuthorizationHandler contra
    // PerfilAcesso.Tipo. A escolha foi deliberada: a matriz Perfil x Permissão não é semeada de
    // propósito (ver RbacSeeder), é preenchida à mão na tela de Controle de Acesso, então criar um
    // código novo deixaria a exclusão travada para todo mundo — inclusive para o Administrador —
    // até alguém marcar a permissão módulo por módulo.
    //
    // Se um dia a exclusão precisar ser delegada a outro perfil (um Gestor QSMS, por exemplo), o
    // caminho é trocar isto por códigos "<modulo>:excluir" e marcá-los na matriz.
    public const string SomenteAdministrador = "perfil:administrador";

    // Liberada para qualquer usuário autenticado: é o endpoint que devolve o próprio perfil e as
    // próprias permissões de quem está logado, e é ele que a tela usa para decidir o que mostrar.
    // Mesmo tratamento já dado a "suporte-ia:usar" e "novidades:usar".
    public const string QualquerUsuarioAutenticado = "usuario:eu";
}
