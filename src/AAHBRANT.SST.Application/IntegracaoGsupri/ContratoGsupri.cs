using FluentValidation;

namespace AAHBRANT.SST.Application.IntegracaoGsupri;

public record RecebimentoGsupriPayload(
    string EventoId, string RecebimentoId, int Versao, string PedidoId, string ObraCodigo,
    string FornecedorDocumento, string NumeroNota, string? ChaveNfe, DateTimeOffset RecebidoEm,
    bool Cancelado, List<ItemGsupriPayload> Itens);

public record ItemGsupriPayload(string ItemId, string ProdutoCodigo, string Descricao, string Unidade,
    decimal QuantidadeRecebida, decimal? QuantidadeLiberada, bool TemNaoConformidade);

public record VincularObraGsupri(string CodigoExterno, Guid ObraId);
public record VincularProdutoGsupri(string CodigoExterno, string Unidade, string Categoria,
    Guid? CatalogoId, string? Tamanho, decimal FatorConversao);
public record ObraGsupriDto(string CodigoExterno, Guid ObraId, string Nome);
public record ProdutoGsupriDto(string CodigoExterno, string Unidade, string Categoria,
    Guid? CatalogoId, string Tamanho, decimal FatorConversao);
public record ItemRecebimentoGsupriDto(string ItemId, string ProdutoCodigo, string Descricao, string Unidade,
    decimal QuantidadeRecebida, decimal? QuantidadeLiberada, int QuantidadeAplicada, string? Pendencia);
public record RecebimentoGsupriDto(Guid Id, string RecebimentoId, int Versao, string PedidoId,
    string ObraCodigo, Guid? ObraId, string NumeroNota, string Status, string? Pendencia,
    DateTimeOffset RecebidoEm, IReadOnlyList<ItemRecebimentoGsupriDto> Itens);
public record PainelGsupriDto(bool Habilitada, IReadOnlyList<ObraGsupriDto> Obras,
    IReadOnlyList<ProdutoGsupriDto> Produtos, IReadOnlyList<RecebimentoGsupriDto> Recebimentos, int TotalRecebimentos);
public record OpcaoGsupriDto(Guid Id, string Nome);
public record OpcoesGsupriDto(IReadOnlyList<OpcaoGsupriDto> Obras, IReadOnlyDictionary<string, List<OpcaoGsupriDto>> Catalogos);

public interface IIntegracaoGsupriService
{
    Task<RecebimentoGsupriDto> ReceberAsync(RecebimentoGsupriPayload dados, CancellationToken ct);
    Task<RecebimentoGsupriDto> ReprocessarAsync(Guid id, CancellationToken ct);
    Task<PainelGsupriDto> PainelAsync(int pagina, CancellationToken ct);
    Task<OpcoesGsupriDto> OpcoesAsync(CancellationToken ct);
    Task VincularObraAsync(VincularObraGsupri dados, CancellationToken ct);
    Task VincularProdutoAsync(VincularProdutoGsupri dados, CancellationToken ct);
}

public class ConflitoGsupriException(string mensagem) : Exception(mensagem);

public class RecebimentoGsupriValidator : AbstractValidator<RecebimentoGsupriPayload>
{
    public RecebimentoGsupriValidator()
    {
        RuleFor(x => x.EventoId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RecebimentoId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Versao).GreaterThan(0);
        RuleFor(x => x.PedidoId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ObraCodigo).NotEmpty().MaximumLength(100);
        RuleFor(x => x.FornecedorDocumento).NotEmpty().MaximumLength(20);
        RuleFor(x => x.NumeroNota).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ChaveNfe).Matches("^[0-9]{44}$").When(x => !string.IsNullOrEmpty(x.ChaveNfe));
        RuleFor(x => x.RecebidoEm).NotEmpty();
        RuleFor(x => x.Itens).NotNull().Must(x => x is { Count: <= 500 }).WithMessage("Máximo de 500 itens por recebimento.");
        RuleFor(x => x.Itens).NotEmpty().When(x => !x.Cancelado);
        RuleFor(x => x.Itens).Must(x => x == null || x.All(i => i != null) &&
            x.Select(i => i.ItemId?.Trim().ToUpperInvariant()).Distinct().Count() == x.Count)
            .WithMessage("Itens nulos ou identificadores repetidos no recebimento.");
        RuleForEach(x => x.Itens).SetValidator(new ItemGsupriValidator());
    }
}

public class ItemGsupriValidator : AbstractValidator<ItemGsupriPayload>
{
    public ItemGsupriValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ProdutoCodigo).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Unidade).NotEmpty().MaximumLength(10);
        RuleFor(x => x.QuantidadeRecebida).InclusiveBetween(0, 100000000).PrecisionScale(18, 6, true);
        RuleFor(x => x.QuantidadeLiberada).GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(x => x.QuantidadeRecebida).PrecisionScale(18, 6, true);
    }
}
