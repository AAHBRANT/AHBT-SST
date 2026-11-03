using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Application.PlanosIntegracao;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.EstoquesEpi.Minimo;

// Painel da aba Estoque (especificação 21/10/2026, Módulos 2 e 3).
public record EstoqueEpiPainelDto(Guid ObraId, string ObraNome, int PrazoReposicaoEpiDias, IReadOnlyList<EstoqueEpiPainelItemDto> Itens);

public record ObterPainelEstoqueEpiQuery(Guid ObraId) : IRequest<EstoqueEpiPainelDto>;

public class ObterPainelEstoqueEpiQueryHandler : IRequestHandler<ObterPainelEstoqueEpiQuery, EstoqueEpiPainelDto>
{
    private readonly IAppDbContext _db;
    public ObterPainelEstoqueEpiQueryHandler(IAppDbContext db) => _db = db;

    public async Task<EstoqueEpiPainelDto> Handle(ObterPainelEstoqueEpiQuery request, CancellationToken ct)
    {
        var obra = await _db.Obras.Where(o => o.Id == request.ObraId).Select(o => new { o.Nome, o.PrazoReposicaoEpiDias }).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Obra não encontrada.");
        var itens = await CalculadoraEstoqueEpi.CalcularAsync(_db, request.ObraId, ct);
        return new EstoqueEpiPainelDto(request.ObraId, obra.Nome, obra.PrazoReposicaoEpiDias, itens);
    }
}

internal static class UsuarioPorObjectId
{
    public static Task<Guid?> ResolverAsync(IAppDbContext db, string? azureAdObjectId, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(azureAdObjectId)
            ? Task.FromResult<Guid?>(null)
            : db.Usuarios.Where(u => u.AzureAdObjectId == azureAdObjectId).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);

    public static async Task<EstoqueEpi> EstoqueAsync(IAppDbContext db, Guid obraId, Guid catalogoEpiId, CancellationToken ct)
    {
        var estoque = await db.EstoquesEpi.FirstOrDefaultAsync(e => e.ObraId == obraId && e.CatalogoEpiId == catalogoEpiId, ct);
        if (estoque is not null) return estoque;
        estoque = new EstoqueEpi { ObraId = obraId, CatalogoEpiId = catalogoEpiId, Saldo = 0 };
        db.EstoquesEpi.Add(estoque);
        return estoque;
    }
}

// Mínimo editável pelo técnico: nulo tira o mínimo (o item para de gerar alerta).
public record DefinirEstoqueMinimoEpiCommand(Guid ObraId, Guid CatalogoEpiId, int? EstoqueMinimo, string? AzureAdObjectId = null) : IRequest;

public class DefinirEstoqueMinimoEpiCommandValidator : AbstractValidator<DefinirEstoqueMinimoEpiCommand>
{
    public DefinirEstoqueMinimoEpiCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.CatalogoEpiId).NotEmpty();
        RuleFor(x => x.EstoqueMinimo).InclusiveBetween(0, 100000).When(x => x.EstoqueMinimo is not null);
    }
}

public class DefinirEstoqueMinimoEpiCommandHandler : IRequestHandler<DefinirEstoqueMinimoEpiCommand>
{
    private readonly IAppDbContext _db;
    private readonly IVerificacaoEstoqueMinimoService? _verificacao;

    public DefinirEstoqueMinimoEpiCommandHandler(IAppDbContext db, IVerificacaoEstoqueMinimoService? verificacao = null)
    {
        _db = db;
        _verificacao = verificacao;
    }

    public async Task Handle(DefinirEstoqueMinimoEpiCommand request, CancellationToken ct)
    {
        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct)) throw new KeyNotFoundException("Obra não encontrada.");
        if (!await _db.CatalogoEpis.AnyAsync(c => c.Id == request.CatalogoEpiId, ct)) throw new KeyNotFoundException("EPI não encontrado.");

        var estoque = await UsuarioPorObjectId.EstoqueAsync(_db, request.ObraId, request.CatalogoEpiId, ct);
        estoque.EstoqueMinimo = request.EstoqueMinimo;
        estoque.EstoqueMinimoDefinidoEm = DateTime.UtcNow;
        estoque.EstoqueMinimoDefinidoPorUsuarioId = await UsuarioPorObjectId.ResolverAsync(_db, request.AzureAdObjectId, ct);
        await _db.SaveChangesAsync(ct);

        if (_verificacao is not null) await _verificacao.VerificarAsync(request.ObraId, ct);
    }
}

// "Aceitar todas as sugestões": aplica o mínimo sugerido em cada EPI da obra que ainda não tem mínimo
// e que tem uso na obra (sugestão maior que zero). Não mexe em mínimo que o técnico já definiu.
public record AplicarSugestoesEstoqueMinimoCommand(Guid ObraId, string? AzureAdObjectId = null) : IRequest<int>;

public class AplicarSugestoesEstoqueMinimoCommandHandler : IRequestHandler<AplicarSugestoesEstoqueMinimoCommand, int>
{
    private readonly IAppDbContext _db;
    private readonly IVerificacaoEstoqueMinimoService? _verificacao;

    public AplicarSugestoesEstoqueMinimoCommandHandler(IAppDbContext db, IVerificacaoEstoqueMinimoService? verificacao = null)
    {
        _db = db;
        _verificacao = verificacao;
    }

    public async Task<int> Handle(AplicarSugestoesEstoqueMinimoCommand request, CancellationToken ct)
    {
        var itens = await CalculadoraEstoqueEpi.CalcularAsync(_db, request.ObraId, ct, comTransferencias: false);
        var usuarioId = await UsuarioPorObjectId.ResolverAsync(_db, request.AzureAdObjectId, ct);
        var aplicados = 0;
        foreach (var item in itens.Where(i => i.EstoqueMinimo is null && i.MinimoSugerido > 0))
        {
            var estoque = await UsuarioPorObjectId.EstoqueAsync(_db, request.ObraId, item.CatalogoEpiId, ct);
            estoque.EstoqueMinimo = item.MinimoSugerido;
            estoque.EstoqueMinimoDefinidoEm = DateTime.UtcNow;
            estoque.EstoqueMinimoDefinidoPorUsuarioId = usuarioId;
            aplicados++;
        }
        await _db.SaveChangesAsync(ct);
        if (aplicados > 0 && _verificacao is not null) await _verificacao.VerificarAsync(request.ObraId, ct);
        return aplicados;
    }
}

public record DefinirPrazoReposicaoEpiCommand(Guid ObraId, int Dias) : IRequest;

public class DefinirPrazoReposicaoEpiCommandValidator : AbstractValidator<DefinirPrazoReposicaoEpiCommand>
{
    public DefinirPrazoReposicaoEpiCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Dias).InclusiveBetween(1, 180).WithMessage("O prazo de reposição deve ficar entre 1 e 180 dias.");
    }
}

public class DefinirPrazoReposicaoEpiCommandHandler : IRequestHandler<DefinirPrazoReposicaoEpiCommand>
{
    private readonly IAppDbContext _db;
    public DefinirPrazoReposicaoEpiCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DefinirPrazoReposicaoEpiCommand request, CancellationToken ct)
    {
        var obra = await _db.Obras.FirstOrDefaultAsync(o => o.Id == request.ObraId, ct) ?? throw new KeyNotFoundException("Obra não encontrada.");
        obra.PrazoReposicaoEpiDias = request.Dias;
        await _db.SaveChangesAsync(ct);
    }
}

// Transferência entre obras: sai do disponível da origem (respeitando os kits separados) e entra na
// outra, com uma movimentação de cada lado para o histórico explicar de onde veio o saldo.
// Correcao = true: lançamento feito na obra errada sendo desfeito — o motivo passa a ser obrigatório
// e o histórico destaca como "Correção de lançamento". As duas pontas levam o mesmo código (TRF-xxxxxxxx)
// na observação; quem fez e quando já ficam em CreatedBy/CreatedAtUtc (AuditableEntity).
public record TransferirEstoqueEpiCommand(Guid CatalogoEpiId, Guid ObraOrigemId, Guid ObraDestinoId, int Quantidade, string? Observacao, bool Correcao = false) : IRequest;

public class TransferirEstoqueEpiCommandValidator : AbstractValidator<TransferirEstoqueEpiCommand>
{
    public TransferirEstoqueEpiCommandValidator()
    {
        RuleFor(x => x.CatalogoEpiId).NotEmpty();
        RuleFor(x => x.ObraOrigemId).NotEmpty();
        RuleFor(x => x.ObraDestinoId).NotEmpty().NotEqual(x => x.ObraOrigemId).WithMessage("Escolha uma obra de destino diferente da origem.");
        RuleFor(x => x.Quantidade).GreaterThan(0);
        RuleFor(x => x.Observacao).MaximumLength(200);
        RuleFor(x => x.Observacao).NotEmpty().When(x => x.Correcao).WithMessage("Informe o motivo da correção.");
    }
}

public class TransferirEstoqueEpiCommandHandler : IRequestHandler<TransferirEstoqueEpiCommand>
{
    private readonly IAppDbContext _db;
    private readonly IVerificacaoEstoqueMinimoService? _verificacao;

    public TransferirEstoqueEpiCommandHandler(IAppDbContext db, IVerificacaoEstoqueMinimoService? verificacao = null)
    {
        _db = db;
        _verificacao = verificacao;
    }

    public async Task Handle(TransferirEstoqueEpiCommand request, CancellationToken ct)
    {
        // Acesso às DUAS obras, de forma explícita: ninguém move estoque de/para obra que não pode ver.
        var origemNome = await _db.GarantirObraNoEscopoAsync(request.ObraOrigemId, ct);
        var destinoNome = await _db.GarantirObraNoEscopoAsync(request.ObraDestinoId, ct);
        if (!await _db.CatalogoEpis.AnyAsync(c => c.Id == request.CatalogoEpiId, ct)) throw new KeyNotFoundException("EPI não encontrado.");

        var origem = await UsuarioPorObjectId.EstoqueAsync(_db, request.ObraOrigemId, request.CatalogoEpiId, ct);
        var reservado = await SituacaoReservasEpi.ReservadoParaOutrosAsync(_db, request.ObraOrigemId, request.CatalogoEpiId, Guid.Empty, ct);
        var disponivel = origem.Saldo - reservado;
        if (disponivel < request.Quantidade)
            throw new InvalidOperationException($"A obra {origemNome} tem {Math.Max(0, disponivel)} unidade(s) disponível(is) deste EPI (saldo {origem.Saldo}, {reservado} separada(s) para kits).");

        var destino = await UsuarioPorObjectId.EstoqueAsync(_db, request.ObraDestinoId, request.CatalogoEpiId, ct);
        var codigo = $"TRF-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var rotulo = request.Correcao ? "Correção de lançamento" : "Transferência";
        var obs = string.IsNullOrWhiteSpace(request.Observacao) ? "" : $" — {request.Observacao.Trim()}";
        origem.Saldo -= request.Quantidade;
        destino.Saldo += request.Quantidade;
        _db.MovimentacoesEstoqueEpi.Add(new MovimentacaoEstoqueEpi
        {
            EstoqueEpiId = origem.Id, Quantidade = request.Quantidade, SaldoResultante = origem.Saldo,
            Tipo = request.Correcao ? TipoMovimentacaoEstoqueEpi.CorrecaoLancamentoSaida : TipoMovimentacaoEstoqueEpi.TransferenciaSaida,
            Observacao = Limitar($"{rotulo} {codigo} para {destinoNome}{obs}"),
        });
        _db.MovimentacoesEstoqueEpi.Add(new MovimentacaoEstoqueEpi
        {
            EstoqueEpiId = destino.Id, Quantidade = request.Quantidade, SaldoResultante = destino.Saldo,
            Tipo = request.Correcao ? TipoMovimentacaoEstoqueEpi.CorrecaoLancamentoEntrada : TipoMovimentacaoEstoqueEpi.TransferenciaEntrada,
            Observacao = Limitar($"{rotulo} {codigo} de {origemNome}{obs}"),
        });
        await _db.SaveChangesAsync(ct);

        if (_verificacao is not null)
        {
            await _verificacao.VerificarAsync(request.ObraOrigemId, ct);
            await _verificacao.VerificarAsync(request.ObraDestinoId, ct);
        }
    }

    // Observacao da movimentação cabe em 300 caracteres (configuração do EF).
    private static string Limitar(string texto) => texto.Length <= 300 ? texto : texto[..300];
}
