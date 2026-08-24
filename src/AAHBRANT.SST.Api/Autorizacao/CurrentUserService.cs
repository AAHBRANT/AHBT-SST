using AAHBRANT.SST.Application.Common.Interfaces;

namespace AAHBRANT.SST.Api.Autorizacao;

// Implementação registrada como Scoped (uma instância por requisição) — ver EscopoPorObraMiddleware,
// que é quem de fato preenche o escopo logo no início do pipeline, e SstDbContext, que lê
// TemAcessoGlobal/ObrasPermitidas no filtro global de escopo por obra.
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
