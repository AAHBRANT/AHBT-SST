using AAHBRANT.SST.Application.Common.Interfaces;

namespace AAHBRANT.SST.Infrastructure.Seguranca;

// Implementação registrada como Scoped em AddInfrastructure — vive aqui (não na Api) para que
// qualquer composition root que chame AddInfrastructure (a Api, mas também o Worker de alertas
// automáticos) resolva SstDbContext sem quebrar, mesmo sem nenhuma requisição HTTP em andamento.
//
// Na Api, EscopoPorObraMiddleware é quem de fato preenche o escopo logo no início do pipeline,
// lendo o usuário autenticado — e SstDbContext lê TemAcessoGlobal/ObrasPermitidas no filtro global
// de escopo por obra. No Worker (processo em segundo plano, sem usuário logado) ninguém chama
// DefinirEscopo, então o padrão abaixo (acesso global) vale sempre — correto: o Worker processa
// vencimentos e escalonamentos de todas as obras, não faz sentido escopar por "usuário atual".
//
// Padrão default = acesso global: se por algum motivo o middleware não rodar antes de uma consulta
// (não deveria acontecer, dada a ordem no Program.cs), o comportamento é o mesmo de hoje — não
// restringe nada — em vez de bloquear tudo silenciosamente. Uma vez a autenticação Entra ID entrar
// em vigor, isso deixa de importar: o middleware sempre roda antes de qualquer controller.
public class CurrentUserService : ICurrentUserService
{
    public bool TemAcessoGlobal { get; private set; } = true;
    public IReadOnlyList<Guid> ObrasPermitidas { get; private set; } = Array.Empty<Guid>();

    public void DefinirEscopo(bool temAcessoGlobal, IReadOnlyList<Guid> obrasPermitidas)
    {
        TemAcessoGlobal = temAcessoGlobal;
        ObrasPermitidas = obrasPermitidas;
    }
}
