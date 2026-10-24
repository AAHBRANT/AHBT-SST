using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Contratos.Commands;

// "Subir contrato" (especificação 21/10/2026): o PDF lido pela IA fica guardado no contrato, para consulta
// e para o checklist da empresa. Um por contrato — enviar de novo substitui.
public record AnexarArquivoContratoCommand(Guid ContratoId, string NomeArquivo, string ContentType, byte[] Conteudo) : IRequest;

public class AnexarArquivoContratoCommandValidator : AbstractValidator<AnexarArquivoContratoCommand>
{
    public const long TamanhoMaximoBytes = 20 * 1024 * 1024;

    public AnexarArquivoContratoCommandValidator()
    {
        RuleFor(x => x.ContratoId).NotEmpty();
        RuleFor(x => x.NomeArquivo).NotEmpty().MaximumLength(260);
        // Confere o conteúdo, não o content-type: alguns navegadores mandam PDF como octet-stream.
        RuleFor(x => x.Conteudo).NotEmpty()
            .Must(EhPdf).WithMessage("Envie o contrato em PDF.")
            .Must(c => c.LongLength <= TamanhoMaximoBytes).WithMessage("O PDF passa de 20 MB.");
    }

    private static bool EhPdf(byte[] conteudo) =>
        conteudo.Length >= 4 && conteudo[0] == '%' && conteudo[1] == 'P' && conteudo[2] == 'D' && conteudo[3] == 'F';
}

public class AnexarArquivoContratoCommandHandler : IRequestHandler<AnexarArquivoContratoCommand>
{
    private readonly IAppDbContext _db;
    public AnexarArquivoContratoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AnexarArquivoContratoCommand request, CancellationToken ct)
    {
        if (!await _db.Contratos.AnyAsync(c => c.Id == request.ContratoId, ct))
            throw new KeyNotFoundException("Contrato não encontrado.");

        var arquivo = await _db.ArquivosContrato.FirstOrDefaultAsync(a => a.ContratoId == request.ContratoId, ct);
        if (arquivo is null)
        {
            arquivo = new ArquivoContrato { ContratoId = request.ContratoId };
            _db.ArquivosContrato.Add(arquivo);
        }
        arquivo.NomeArquivo = request.NomeArquivo;
        arquivo.ContentType = "application/pdf";
        arquivo.Conteudo = request.Conteudo;
        arquivo.TamanhoBytes = request.Conteudo.LongLength;
        await _db.SaveChangesAsync(ct);
    }
}

public record ArquivoContratoDto(string NomeArquivo, string ContentType, byte[] Conteudo);

public record ObterArquivoContratoQuery(Guid ContratoId) : IRequest<ArquivoContratoDto?>;

public class ObterArquivoContratoQueryHandler : IRequestHandler<ObterArquivoContratoQuery, ArquivoContratoDto?>
{
    private readonly IAppDbContext _db;
    public ObterArquivoContratoQueryHandler(IAppDbContext db) => _db = db;

    // Passa pelo Contrato (com filtro de ativo) para um contrato excluído não continuar servindo o PDF.
    public Task<ArquivoContratoDto?> Handle(ObterArquivoContratoQuery request, CancellationToken ct) =>
        _db.Contratos
            .Where(c => c.Id == request.ContratoId && c.Arquivo != null)
            .Select(c => new ArquivoContratoDto(c.Arquivo!.NomeArquivo, c.Arquivo.ContentType, c.Arquivo.Conteudo))
            .FirstOrDefaultAsync(ct);
}
