using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Atividades.DetalhesOs;

// Resolver as pendências da Ordem de Serviço em lote, sem sair da OS (17/11/2026): vincular procedimentos a
// várias atividades de uma vez e aceitar/descartar as sugestões da IA (medidas por fase e itens da OS) em bloco.
// Mesmas regras dos comandos unitários: só procedimento/IT vigente; quem decide vem do token; escopo por obra.

public static class LimitesLoteOs
{
    public const int MaxAtividades = 200;
    public const int MaxSugestoes = 1000;
}

// ---- Procedimentos ----

public record ProcedimentosAtividadeDto(Guid AtividadeId, string AtividadeNome, List<Guid> DocumentoIds);

public record ObterProcedimentosAtividadesQuery(List<Guid> AtividadeIds) : IRequest<List<ProcedimentosAtividadeDto>>;

public class ObterProcedimentosAtividadesQueryValidator : AbstractValidator<ObterProcedimentosAtividadesQuery>
{
    public ObterProcedimentosAtividadesQueryValidator()
        => RuleFor(x => x.AtividadeIds).NotNull().Must(l => l.Count <= LimitesLoteOs.MaxAtividades);
}

public class ObterProcedimentosAtividadesQueryHandler : IRequestHandler<ObterProcedimentosAtividadesQuery, List<ProcedimentosAtividadeDto>>
{
    private readonly IAppDbContext _db;

    public ObterProcedimentosAtividadesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<ProcedimentosAtividadeDto>> Handle(ObterProcedimentosAtividadesQuery request, CancellationToken ct)
    {
        var ids = request.AtividadeIds.Distinct().ToList();
        var atividades = await _db.Atividades.NoEscopoDaAtividade(_db, a => a.Id)
            .Where(a => ids.Contains(a.Id))
            .Select(a => new
            {
                a.Id,
                a.Nome,
                // Só os vigentes: são os únicos que contam para a OS e os únicos que a tela oferece.
                Docs = a.Documentos.Where(d => d.MaterialApoio!.Situacao == SituacaoDocumentoSst.Vigente)
                    .Select(d => d.MaterialApoioId).ToList(),
            })
            .ToListAsync(ct);

        // Mantém a ordem pedida (a mesma da OS).
        return ids.Select(id => atividades.FirstOrDefault(a => a.Id == id))
            .Where(a => a is not null)
            .Select(a => new ProcedimentosAtividadeDto(a!.Id, a.Nome, a.Docs))
            .ToList();
    }
}

public record VinculoProcedimentosAtividade(Guid AtividadeId, List<Guid> DocumentoIds);

// Substitui, em cada atividade, os vínculos com procedimentos vigentes pela lista informada. Vínculos com
// documento que deixou de ser vigente ficam como estão (não aparecem na tela e não contam na OS).
public record VincularProcedimentosEmLoteCommand(List<VinculoProcedimentosAtividade> Vinculos) : IRequest;

public class VincularProcedimentosEmLoteCommandValidator : AbstractValidator<VincularProcedimentosEmLoteCommand>
{
    public VincularProcedimentosEmLoteCommandValidator()
    {
        RuleFor(x => x.Vinculos).NotEmpty().Must(l => l.Count <= LimitesLoteOs.MaxAtividades);
        RuleForEach(x => x.Vinculos).ChildRules(v =>
        {
            v.RuleFor(x => x.AtividadeId).NotEmpty();
            v.RuleFor(x => x.DocumentoIds).NotNull();
        });
    }
}

public class VincularProcedimentosEmLoteCommandHandler : IRequestHandler<VincularProcedimentosEmLoteCommand>
{
    private readonly IAppDbContext _db;

    public VincularProcedimentosEmLoteCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(VincularProcedimentosEmLoteCommand request, CancellationToken ct)
    {
        var porAtividade = request.Vinculos
            .GroupBy(v => v.AtividadeId)
            .ToDictionary(g => g.Key, g => g.SelectMany(v => v.DocumentoIds).Distinct().ToList());
        var atividadeIds = porAtividade.Keys.ToList();

        var atividades = await _db.Atividades.NoEscopoDaAtividade(_db, a => a.Id)
            .Include(a => a.Documentos).ThenInclude(d => d.MaterialApoio)
            .Where(a => atividadeIds.Contains(a.Id))
            .ToListAsync(ct);
        if (atividades.Count != atividadeIds.Count)
            throw new KeyNotFoundException("Atividade não encontrada.");

        var docIds = porAtividade.Values.SelectMany(l => l).Distinct().ToList();
        var vigentes = await _db.MateriaisApoio
            .Where(m => docIds.Contains(m.Id)
                && (m.Tipo == TipoDocumentoSst.Procedimento || m.Tipo == TipoDocumentoSst.InstrucaoTrabalho)
                && m.Situacao == SituacaoDocumentoSst.Vigente)
            .Select(m => m.Id).ToListAsync(ct);
        if (vigentes.Count != docIds.Count)
            throw new InvalidOperationException("Só procedimento ou instrução de trabalho vigente pode ser vinculado à atividade.");

        foreach (var a in atividades)
        {
            var desejados = porAtividade[a.Id];
            foreach (var d in a.Documentos
                         .Where(d => d.MaterialApoio?.Situacao == SituacaoDocumentoSst.Vigente && !desejados.Contains(d.MaterialApoioId))
                         .ToList())
                _db.AtividadeDocumentos.Remove(d);
            foreach (var id in desejados.Where(id => a.Documentos.All(d => d.MaterialApoioId != id)))
                _db.AtividadeDocumentos.Add(new AtividadeDocumento { AtividadeId = a.Id, MaterialApoioId = id });
        }

        await _db.SaveChangesAsync(ct);
    }
}

// ---- Sugestões da IA ----

public enum OrigemSugestaoOs
{
    Medida = 1,
    ItemOs = 2,
}

// Categoria: fase da medida (Antes/Durante/Após) ou tipo do item da OS (Interromper/Restrição/Autorização).
public record SugestaoPendenteOsDto(
    Guid Id, OrigemSugestaoOs Origem, Guid AtividadeId, string AtividadeNome, string? Perigo, string Categoria, string Texto, string? Requisito);

public record ListarSugestoesPendentesOsQuery(List<Guid> AtividadeIds) : IRequest<List<SugestaoPendenteOsDto>>;

public class ListarSugestoesPendentesOsQueryValidator : AbstractValidator<ListarSugestoesPendentesOsQuery>
{
    public ListarSugestoesPendentesOsQueryValidator()
        => RuleFor(x => x.AtividadeIds).NotNull().Must(l => l.Count <= LimitesLoteOs.MaxAtividades);
}

public class ListarSugestoesPendentesOsQueryHandler : IRequestHandler<ListarSugestoesPendentesOsQuery, List<SugestaoPendenteOsDto>>
{
    private readonly IAppDbContext _db;

    public ListarSugestoesPendentesOsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<SugestaoPendenteOsDto>> Handle(ListarSugestoesPendentesOsQuery request, CancellationToken ct)
    {
        var ids = request.AtividadeIds.Distinct().ToList();
        var atividades = await _db.Atividades.NoEscopoDaAtividade(_db, a => a.Id)
            .Where(a => ids.Contains(a.Id))
            .Select(a => new { a.Id, a.Nome }).ToListAsync(ct);
        var nomes = atividades.ToDictionary(a => a.Id, a => a.Nome);
        var permitidas = nomes.Keys.ToList();

        var medidas = await _db.MedidasPrevencao
            .Where(m => m.Status == StatusMedidaPrevencao.Pendente && permitidas.Contains(m.Risco!.AtividadeId))
            .Select(m => new { m.Id, m.Risco!.AtividadeId, Perigo = m.Risco.Perigo!.Nome, m.Fase, m.Texto, m.Ordem })
            .ToListAsync(ct);
        var itens = await _db.AtividadeItensOs
            .Where(i => i.Status == StatusMedidaPrevencao.Pendente && permitidas.Contains(i.AtividadeId))
            .Select(i => new { i.Id, i.AtividadeId, i.Tipo, i.Texto, i.Requisito, i.CreatedAtUtc })
            .ToListAsync(ct);

        var lista = new List<(int Ordem, int Sub, SugestaoPendenteOsDto Dto)>();
        foreach (var m in medidas)
            lista.Add((ids.IndexOf(m.AtividadeId), (int)m.Fase * 1000 + m.Ordem,
                new(m.Id, OrigemSugestaoOs.Medida, m.AtividadeId, nomes[m.AtividadeId], m.Perigo, RotuloFase(m.Fase), m.Texto, null)));
        foreach (var i in itens)
            lista.Add((ids.IndexOf(i.AtividadeId), 10_000 + (int)i.Tipo,
                new(i.Id, OrigemSugestaoOs.ItemOs, i.AtividadeId, nomes[i.AtividadeId], null, RotuloTipo(i.Tipo), i.Texto, i.Requisito)));

        return lista.OrderBy(x => x.Ordem).ThenBy(x => x.Dto.Perigo).ThenBy(x => x.Sub).Select(x => x.Dto).ToList();
    }

    private static string RotuloFase(FaseMedidaPrevencao f) => f switch
    {
        FaseMedidaPrevencao.Antes => "Antes do trabalho",
        FaseMedidaPrevencao.Durante => "Durante o trabalho",
        FaseMedidaPrevencao.Apos => "Após o trabalho",
        _ => f.ToString(),
    };

    private static string RotuloTipo(TipoItemAtividadeOs t) => t switch
    {
        TipoItemAtividadeOs.InterromperQuando => "Interromper e avisar a liderança quando",
        TipoItemAtividadeOs.Restricao => "Não executar sem",
        TipoItemAtividadeOs.Autorizacao => "Autorização exigida",
        _ => t.ToString(),
    };
}

// Aceita ou descarta, de uma vez, as sugestões ainda pendentes. O que já foi decidido (por outra pessoa, em
// outra aba) é ignorado; devolve quantas foram de fato decididas.
public record DecidirSugestoesOsEmLoteCommand(List<Guid> MedidaIds, List<Guid> ItemIds, bool Aceitar, Guid? UsuarioId, string? NomeUsuario)
    : IRequest<int>;

public class DecidirSugestoesOsEmLoteCommandValidator : AbstractValidator<DecidirSugestoesOsEmLoteCommand>
{
    public DecidirSugestoesOsEmLoteCommandValidator()
    {
        RuleFor(x => x.MedidaIds).NotNull();
        RuleFor(x => x.ItemIds).NotNull();
        RuleFor(x => x.MedidaIds.Count + x.ItemIds.Count).InclusiveBetween(1, LimitesLoteOs.MaxSugestoes)
            .WithName("Sugestões").WithMessage($"Selecione de 1 a {LimitesLoteOs.MaxSugestoes} sugestões.");
    }
}

public class DecidirSugestoesOsEmLoteCommandHandler : IRequestHandler<DecidirSugestoesOsEmLoteCommand, int>
{
    private readonly IAppDbContext _db;

    public DecidirSugestoesOsEmLoteCommandHandler(IAppDbContext db) => _db = db;

    public async Task<int> Handle(DecidirSugestoesOsEmLoteCommand request, CancellationToken ct)
    {
        var medidaIds = request.MedidaIds.Distinct().ToList();
        var itemIds = request.ItemIds.Distinct().ToList();

        var riscosNoEscopo = _db.Riscos.NoEscopoDaAtividade(_db, r => r.AtividadeId).Select(r => r.Id);
        var medidas = medidaIds.Count == 0 ? new List<MedidaPrevencao>() : await _db.MedidasPrevencao
            .Where(m => medidaIds.Contains(m.Id) && m.Status == StatusMedidaPrevencao.Pendente && riscosNoEscopo.Contains(m.RiscoId))
            .ToListAsync(ct);
        var itens = itemIds.Count == 0 ? new List<AtividadeItemOs>() : await _db.AtividadeItensOs.NoEscopoDaAtividade(_db, i => i.AtividadeId)
            .Where(i => itemIds.Contains(i.Id) && i.Status == StatusMedidaPrevencao.Pendente)
            .ToListAsync(ct);

        var status = request.Aceitar ? StatusMedidaPrevencao.Validada : StatusMedidaPrevencao.Descartada;
        var agora = DateTime.UtcNow;
        var nome = await NomeUsuarioDetalhesOs.ResolverAsync(_db, request.UsuarioId, request.NomeUsuario, ct);

        foreach (var m in medidas)
        {
            m.Status = status;
            m.ValidadoEmUtc = agora;
            m.ValidadoPorUsuarioId = request.UsuarioId;
            m.ValidadoPorNome = nome;
        }
        foreach (var i in itens)
        {
            i.Status = status;
            i.ValidadoEmUtc = agora;
            i.ValidadoPorUsuarioId = request.UsuarioId;
            i.ValidadoPorNome = nome;
        }

        await _db.SaveChangesAsync(ct);
        return medidas.Count + itens.Count;
    }
}
