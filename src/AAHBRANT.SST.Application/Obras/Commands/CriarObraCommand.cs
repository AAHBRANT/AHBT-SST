using AAHBRANT.SST.Application.Common;
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
    string LogoContentType,
    string? Cep = null,
    string? Cnae = null,
    int? GrauRisco = null) : IRequest<Guid>;

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
        RuleFor(x => x.Cep).MaximumLength(10);
        RuleFor(x => x.Cnpj).MaximumLength(18);
        RuleFor(x => x.Cnae).MaximumLength(200);
        RuleFor(x => x.GrauRisco).InclusiveBetween(1, 4).When(x => x.GrauRisco.HasValue);
        RuleFor(x => x.LogoConteudo)
            .NotEmpty().WithMessage("A logomarca da obra é obrigatória.")
            .Must(f => f.Length <= ValidacaoLogoObra.TamanhoMaximoBytes).WithMessage("A logomarca deve ter no máximo 5 MB.")
            .Must((comando, conteudo) => ValidadorAssinaturaArquivo.AssinaturaConfere(conteudo, comando.LogoContentType))
                .WithMessage("O conteúdo do arquivo não corresponde ao tipo declarado.");
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
            Cliente = string.IsNullOrWhiteSpace(request.Cliente) ? null : request.Cliente.Trim(),
            Status = request.Status,
            DataInicio = request.DataInicio,
            DataPrevisaoTermino = request.DataPrevisaoTermino,
            Endereco = string.IsNullOrWhiteSpace(request.Endereco) ? null : request.Endereco.Trim(),
            Cidade = string.IsNullOrWhiteSpace(request.Cidade) ? null : request.Cidade.Trim(),
            Uf = string.IsNullOrWhiteSpace(request.Uf) ? null : request.Uf.Trim(),
            Cep = string.IsNullOrWhiteSpace(request.Cep) ? null : request.Cep.Trim(),
            Cnpj = string.IsNullOrWhiteSpace(request.Cnpj) ? null : request.Cnpj.Trim(),
            Cnae = string.IsNullOrWhiteSpace(request.Cnae) ? null : request.Cnae.Trim(),
            GrauRisco = request.GrauRisco,
            LogoConteudo = request.LogoConteudo,
            LogoContentType = request.LogoContentType
        };

        _db.Obras.Add(obra);
        await _db.SaveChangesAsync(ct);
        return obra.Id;
    }
}
