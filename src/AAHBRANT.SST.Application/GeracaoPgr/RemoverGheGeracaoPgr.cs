using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.MatrizRisco;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.GeracaoPgr;

// Remove um GHE do rascunho do PGR (03/11/2026). As funções, os riscos e as medidas moram dentro do GHE e saem com ele.
// As atividades da EAP vão para o GHE de destino (com os riscos) ou são descartadas; descartadas, voltam a aparecer
// como "sem GHE" nas pendências. Depois, as ações do plano que nasceram das medidas e deixaram de ter risco que as
// justifique também saem: ação escrita à mão, com texto editado, com responsável ou com datas fica. Os GHEs que
// ficam são renumerados em sequência (04/11/2026): remover o GHE 02 de 4 faz o 03 virar 02 e o 04 virar 03, sem buraco.
public record RemoverGheGeracaoPgrCommand(Guid Id, int GheNumero, int? DestinoGheNumero) : IRequest<RemoverGheResultado>;

public record RemoverGheResultado(int Funcoes, int Riscos, int AcoesRemovidas);

public class RemoverGheGeracaoPgrCommandHandler : IRequestHandler<RemoverGheGeracaoPgrCommand, RemoverGheResultado>
{
    private readonly IAppDbContext _db;

    public RemoverGheGeracaoPgrCommandHandler(IAppDbContext db) => _db = db;

    public async Task<RemoverGheResultado> Handle(RemoverGheGeracaoPgrCommand request, CancellationToken ct)
    {
        var g = await SerializacaoGeracao.CarregarAsync(_db, request.Id, ct);
        SerializacaoGeracao.GarantirEditavel(g);
        var e = SerializacaoGeracao.Ler(g.EstruturaJson);

        var matriz = await _db.MatrizRiscoConfigs.Include(c => c.Celulas).VigenteMaisRecente().FirstOrDefaultAsync(ct);
        var celulas = matriz?.Celulas.Select(c => (c.Probabilidade, c.Severidade, c.NivelRisco)).ToList();

        var resultado = Remover(e, request.GheNumero, request.DestinoGheNumero, celulas);
        g.EstruturaJson = SerializacaoGeracao.Gravar(e);
        await _db.SaveChangesAsync(ct);
        return resultado;
    }

    // Separado do handler para testar sem banco. Altera a estrutura recebida. Sem matriz não há como saber quais
    // ações ainda se justificam, então o plano não é tocado.
    public static RemoverGheResultado Remover(EstruturaGeracaoPgr e, int gheNumero, int? destinoNumero,
        IReadOnlyList<(int P, int S, NivelRisco Nivel)>? matriz)
    {
        var ghe = e.Ghes.FirstOrDefault(x => x.Numero == gheNumero)
            ?? throw new KeyNotFoundException("GHE não encontrado no rascunho.");

        GheGerado? destino = null;
        if (destinoNumero is not null)
        {
            if (destinoNumero == gheNumero) throw new InvalidOperationException("O GHE de destino precisa ser outro.");
            destino = e.Ghes.FirstOrDefault(x => x.Numero == destinoNumero)
                ?? throw new KeyNotFoundException("GHE de destino não encontrado no rascunho.");
        }

        var funcoes = ghe.Funcoes.Count;
        var riscos = ghe.Atividades.SelectMany(a => a.Riscos).Count(r => !r.Excluido);

        // Movidas, as atividades levam os riscos; o que sai de fato é só o que ficou no GHE removido.
        if (destino is not null)
        {
            destino.Atividades.AddRange(ghe.Atividades);
            riscos = 0;
        }
        e.Ghes.Remove(ghe);
        RenumerarSemBuraco(e);

        var removidas = 0;
        if (matriz is not null)
        {
            var vigentes = SugerirAcoesMelhoriaPlanoGeracaoPgrCommandHandler.DescricoesDasMedidas(e, matriz)
                .Select(FuncaoSstClassifier.Normalizar).ToHashSet();
            removidas = e.AcoesPlano.RemoveAll(a => AcaoSoGerada(a) && !vigentes.Contains(FuncaoSstClassifier.Normalizar(a.Descricao)));
        }
        return new RemoverGheResultado(funcoes, riscos, removidas);
    }

    // Numera os GHEs 1, 2, 3... pela ordem atual. Devolve se alguma numeração mudou (rascunho que ficou com buraco).
    public static bool RenumerarSemBuraco(EstruturaGeracaoPgr e)
    {
        var mudou = false;
        var proximo = 1;
        foreach (var ghe in e.Ghes.OrderBy(x => x.Numero))
        {
            if (ghe.Numero != proximo) mudou = true;
            ghe.Numero = proximo++;
        }
        return mudou;
    }

    private static bool AcaoSoGerada(AcaoPlanoGerada a)
        => !string.IsNullOrWhiteSpace(a.DescricaoGerada)
           && FuncaoSstClassifier.Normalizar(a.Descricao) == FuncaoSstClassifier.Normalizar(a.DescricaoGerada)
           && a.ResponsavelUsuarioId is null && string.IsNullOrWhiteSpace(a.ResponsavelNome)
           && a.Inicio is null && a.Prazo is null;
}
