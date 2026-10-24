using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.MateriaisApoio.Commands;

public record CriarMaterialApoioCommand(
    string Nome,
    string? Categoria,
    string NomeArquivo,
    string ContentType,
    byte[] Conteudo,
    TipoDocumentoSst Tipo = TipoDocumentoSst.MaterialApoio,
    string? Codigo = null,
    string? Revisao = null,
    SituacaoDocumentoSst? Situacao = null,
    string? FormaConsulta = null,
    bool IncluirEmTodasOs = false) : IRequest<Guid>;

public class CriarMaterialApoioCommandValidator : AbstractValidator<CriarMaterialApoioCommand>
{
    // Mesmo vocabulário aceito por ValidadorAssinaturaArquivo — imagens (pôsteres/sinalização),
    // PDF e docx (instruções técnicas), cobrindo os 4 arquivos já existentes na pasta ALOJAMENTO.
    private static readonly string[] TiposPermitidos =
    {
        "image/jpeg",
        "image/png",
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    };

    private const int TamanhoMaximoBytes = 20 * 1024 * 1024;

    public CriarMaterialApoioCommandValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Categoria).MaximumLength(100);
        RuleFor(x => x.NomeArquivo).NotEmpty().MaximumLength(260);
        RuleFor(x => x.ContentType).Must(t => TiposPermitidos.Contains(t))
            .WithMessage("Tipo de arquivo não suportado. Envie imagem (JPEG/PNG), PDF ou Word (.docx).");
        RuleFor(x => x.Tipo).IsInEnum();
        // Documento controlado (procedimento, IT, política) precisa de código, revisão e situação.
        When(x => x.Tipo != TipoDocumentoSst.MaterialApoio, () =>
        {
            RuleFor(x => x.Codigo).NotEmpty().WithMessage("Informe o código SGI.").MaximumLength(60);
            RuleFor(x => x.Revisao).NotEmpty().WithMessage("Informe a revisão.").MaximumLength(20);
            RuleFor(x => x.Situacao).NotNull().WithMessage("Informe a situação.");
        });
        RuleFor(x => x.FormaConsulta).MaximumLength(300);
        RuleFor(x => x.IncluirEmTodasOs).Equal(false).When(x => x.Tipo != TipoDocumentoSst.Politica)
            .WithMessage("Só política corporativa pode entrar em todas as Ordens de Serviço.");
        RuleFor(x => x.Conteudo)
            .NotEmpty().WithMessage("O arquivo é obrigatório.")
            .Must(c => c.Length <= TamanhoMaximoBytes).WithMessage("O arquivo deve ter no máximo 20 MB.")
            .Must((comando, conteudo) => ValidadorAssinaturaArquivo.AssinaturaConfere(conteudo, comando.ContentType))
                .WithMessage("O conteúdo do arquivo não corresponde ao tipo declarado.");
    }
}

public class CriarMaterialApoioCommandHandler : IRequestHandler<CriarMaterialApoioCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarMaterialApoioCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarMaterialApoioCommand request, CancellationToken ct)
    {
        var material = new MaterialApoio
        {
            Nome = request.Nome,
            Categoria = request.Categoria,
            NomeArquivo = request.NomeArquivo,
            ContentType = request.ContentType,
            Conteudo = request.Conteudo,
            Tipo = request.Tipo,
            Codigo = request.Tipo == TipoDocumentoSst.MaterialApoio ? null : request.Codigo?.Trim(),
            Revisao = request.Tipo == TipoDocumentoSst.MaterialApoio ? null : request.Revisao?.Trim(),
            Situacao = request.Tipo == TipoDocumentoSst.MaterialApoio ? null : request.Situacao,
            FormaConsulta = request.FormaConsulta?.Trim(),
            IncluirEmTodasOs = request.IncluirEmTodasOs,
        };

        _db.MateriaisApoio.Add(material);
        await _db.SaveChangesAsync(ct);
        return material.Id;
    }
}
