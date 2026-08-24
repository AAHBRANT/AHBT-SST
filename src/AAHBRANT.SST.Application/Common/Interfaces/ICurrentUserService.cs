namespace AAHBRANT.SST.Application.Common.Interfaces;

// Abstração do usuário autenticado da requisição atual — usada pelo filtro de escopo por obra
// (SstDbContext, camada 3 de docs/RBAC-Matrix.md §4: "Global Query Filter... para que nenhum
// endpoint dependa de lembrar de filtrar manualmente por obra — mitiga BOLA"). A interface mora
// na Application (para a Infrastructure poder depender dela sem violar Clean Architecture, mesmo
// motivo de IAppDbContext); a implementação (CurrentUserService, na Api) depende de HttpContext.
//
// Populada por EscopoPorObraMiddleware uma vez por requisição, ANTES de qualquer consulta ao
// DbContext — os getters aqui são só leitura do que o middleware já resolveu, nunca fazem I/O.
public interface ICurrentUserService
{
    // true = não restringir por obra: autenticação Entra ID ainda desligada (API roda sem auth
    // real até confirmação explícita do usuário para provisionar o App Registration — ver
    // Program.cs — comportamento IDÊNTICO ao de hoje, sem regressão), OU o usuário tem alguma
    // atribuição de perfil com escopo Global/Unidade (UsuarioPerfilObra.ObraId nulo).
    bool TemAcessoGlobal { get; }

    // Obras às quais o usuário tem atribuição específica (UsuarioPerfilObra.ObraId preenchido).
    // Só é relevante quando TemAcessoGlobal é false.
    IReadOnlyList<Guid> ObrasPermitidas { get; }

    void DefinirEscopo(bool temAcessoGlobal, IReadOnlyList<Guid> obrasPermitidas);
}
