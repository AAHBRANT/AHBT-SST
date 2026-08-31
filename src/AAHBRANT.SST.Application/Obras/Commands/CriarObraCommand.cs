using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Obras;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Obras.Commands;

public record CriarObraCommand(
    string Codigo,
    string Nome,
    string? Cliente,
    StatusObra Status,
    DateTime? DataInicio,
    DateTime? DataPrevisaoTermino,
    string? Endereco,
    string? Cidade,
    string? Uf,
    string? Cnpj,
    byte[] LogoConteudo,
    string LogoContentType) : IRequest<Guid>;

// Logomarca obrigatória no cadastro (decisão do usuário, 31/08): a obra só é considerada
// finalizada com a logo anexada, pois ela é aplicada no layout padrão dos documentos gerados
// e assinados (APR, PT, DDS, Ficha de EPI, Relatório de Fiscalização). Mesma restrição de
// tamanho/tipo do anexo posterior (AnexarLogoObraCommand), vinda de ValidacaoLogoObra.
public class CriarObraCommandValidator : AbstractValidator<CriarObraCommand>
{
    public CriarObraCommandValidator()
    {
        RuleFor(x => x.Codigo).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Uf).MaximumLength(2);
        RuleFor(x => x.Cnpj).MaximumLength(18);
        RuleFor(x => x.LogoConteudo)
            .NotEmpty().WithMessage("A logomarca da obra é obrigatória.")
            .Must(f => f.Length <= ValidacaoLogoObra.TamanhoMaximoBytes).WithMessage("A logomarca deve ter no máximo 5 MB.");
        RuleFor(x => x.LogoContentType)
            .Must(t => ValidacaoLogoObra.TiposPermitidos.Contains(t)).WithMessage("A logomarca deve ser um arquivo JPEG ou PNG.");
    }
}

public class CriarObraCommandHandler : IRequestHandler<CriarObraCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarObraCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarObraCommand request, CancellationToken ct)
    {
        var obra = new Obra
        {
            Codigo = request.Codigo,
            Nome = request.Nome,
            Cliente = request.Cliente,
            Status = request.Status,
            DataInicio = request.DataInicio,
            DataPrevisaoTermino = request.DataPrevisaoTermino,
            Endereco = request.Endereco,
            Cidade = request.Cidade,
            Uf = request.Uf,
            Cnpj = request.Cnpj,
            LogoConteudo = request.LogoConteudo,
            LogoContentType = request.LogoContentType
        };

        _db.Obras.Add(obra);
        await _db.SaveChangesAsync(ct);
        return obra.Id;
    }
}
