using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Queries;

// Canal novo (15/09/2026): o G-RH ainda não tem foto de ninguém, então busca aqui pra quem já
// foi fotografado no cadastro de EPI/ASO do SST — usado só pelo endpoint de integração
// app-to-app (ver IntegracaoGrhController), nunca por uma tela do SST em si. Por CPF (não por
// Id) porque o G-RH não conhece o Guid interno do Trabalhador, só o CPF que os dois sistemas
// compartilham. Mesmo ICpfHashService já usado por SincronizarColaboradorGrhCommand.
//
// Sem IgnoreQueryFilters: a App Role Sst.LerFotos (ver AppRolesReconhecidas) já dá acesso
// global via EscopoPorObraMiddleware, então o filtro global (Trabalhador.Ativo && (acesso
// global || obra permitida)) se aplica normalmente e já faz o corte certo sozinho.
public record ObterFotoTrabalhadorPorCpfQuery(string Cpf) : IRequest<FotoTrabalhadorResultado?>;

public class ObterFotoTrabalhadorPorCpfQueryHandler : IRequestHandler<ObterFotoTrabalhadorPorCpfQuery, FotoTrabalhadorResultado?>
{
    private readonly IAppDbContext _db;
    private readonly ICpfHashService _cpfHash;

    public ObterFotoTrabalhadorPorCpfQueryHandler(IAppDbContext db, ICpfHashService cpfHash)
    {
        _db = db;
        _cpfHash = cpfHash;
    }

    public async Task<FotoTrabalhadorResultado?> Handle(ObterFotoTrabalhadorPorCpfQuery request, CancellationToken ct)
    {
        var hash = _cpfHash.CalcularHash(request.Cpf);
        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.CpfHash == hash, ct);

        if (trabalhador is null || trabalhador.FotoConteudo is null || trabalhador.FotoConteudo.Length == 0) return null;

        var extensao = trabalhador.FotoContentType == "image/png" ? "png" : "jpg";
        return new FotoTrabalhadorResultado
        {
            Conteudo = trabalhador.FotoConteudo,
            ContentType = string.IsNullOrEmpty(trabalhador.FotoContentType) ? "application/octet-stream" : trabalhador.FotoContentType,
            NomeArquivo = $"trabalhador-{trabalhador.Id}-foto.{extensao}",
        };
    }
}
