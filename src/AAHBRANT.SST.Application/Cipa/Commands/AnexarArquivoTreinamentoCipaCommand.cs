using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Commands;

// Um único command cobre os dois anexos do treinamento (certificado / lista de presença) — mesma
// entidade, mesmas regras de validação de arquivo, só muda qual par de colunas é escrito.
public enum TipoArquivoTreinamentoCipa { Certificado, ListaPresenca }

public record AnexarArquivoTreinamentoCipaCommand(
    Guid TreinamentoId,
    TipoArquivoTreinamentoCipa Tipo,
    byte[] Conteudo,
    string ContentType) : IRequest;

public class AnexarArquivoTreinamentoCipaCommandValidator : AbstractValidator<AnexarArquivoTreinamentoCipaCommand>
{
    private static readonly string[] TiposPermitidos = { "image/jpeg", "image/png", "application/pdf" };
    private const int TamanhoMaximoBytes = 8 * 1024 * 1024;

    public AnexarArquivoTreinamentoCipaCommandValidator()
    {
        RuleFor(x => x.TreinamentoId).NotEmpty();
        RuleFor(x => x.Conteudo)
            .NotEmpty().WithMessage("O arquivo é obrigatório.")
            .Must(f => f.Length <= TamanhoMaximoBytes).WithMessage("O arquivo deve ter no máximo 8 MB.")
            .Must((comando, conteudo) => ValidadorAssinaturaArquivo.AssinaturaConfere(conteudo, comando.ContentType))
                .WithMessage("O conteúdo do arquivo não corresponde ao tipo declarado.");
        RuleFor(x => x.ContentType)
            .Must(t => TiposPermitidos.Contains(t)).WithMessage("O arquivo deve ser JPEG, PNG ou PDF.");
    }
}

public class AnexarArquivoTreinamentoCipaCommandHandler : IRequestHandler<AnexarArquivoTreinamentoCipaCommand>
{
    private readonly IAppDbContext _db;

    public AnexarArquivoTreinamentoCipaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AnexarArquivoTreinamentoCipaCommand request, CancellationToken ct)
    {
        var treinamento = await _db.TreinamentosCipa.FirstOrDefaultAsync(t => t.Id == request.TreinamentoId, ct)
            ?? throw new KeyNotFoundException($"Treinamento {request.TreinamentoId} não encontrado.");

        if (request.Tipo == TipoArquivoTreinamentoCipa.Certificado)
        {
            treinamento.CertificadoConteudo = request.Conteudo;
            treinamento.CertificadoContentType = request.ContentType;
        }
        else
        {
            treinamento.ListaPresencaConteudo = request.Conteudo;
            treinamento.ListaPresencaContentType = request.ContentType;
        }

        await _db.SaveChangesAsync(ct);
    }
}
