using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Asos.Commands;

// Anexa (ou substitui) o PDF/foto do ASO — criado para a importação da pasta da terceirizada (23/10/2026),
// em que o ASO de cada funcionário chega pronto, emitido pela clínica da própria empresa. Mesmo padrão de
// AnexarArquivoCertificadoTreinamentoCommand.
public record AnexarArquivoAsoCommand(Guid AsoId, string NomeArquivo, byte[] Conteudo, string ContentType) : IRequest<Guid>;

public class AnexarArquivoAsoCommandValidator : AbstractValidator<AnexarArquivoAsoCommand>
{
    private static readonly string[] TiposPermitidos = { "application/pdf", "image/jpeg", "image/png" };
    private const int TamanhoMaximoBytes = 10 * 1024 * 1024;

    public AnexarArquivoAsoCommandValidator()
    {
        RuleFor(x => x.AsoId).NotEmpty();
        RuleFor(x => x.NomeArquivo).NotEmpty().MaximumLength(260);
        RuleFor(x => x.ContentType)
            .Must(t => TiposPermitidos.Contains(t))
            .WithMessage("O ASO deve ser um arquivo PDF, JPEG ou PNG.");
        RuleFor(x => x.Conteudo)
            .NotEmpty().WithMessage("O arquivo do ASO é obrigatório.")
            .Must(c => c.Length <= TamanhoMaximoBytes).WithMessage("O arquivo deve ter no máximo 10 MB.")
            .Must((comando, conteudo) => ValidadorAssinaturaArquivo.AssinaturaConfere(conteudo, comando.ContentType))
                .WithMessage("O conteúdo do arquivo não corresponde ao tipo declarado.");
    }
}

public class AnexarArquivoAsoCommandHandler : IRequestHandler<AnexarArquivoAsoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public AnexarArquivoAsoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(AnexarArquivoAsoCommand request, CancellationToken ct)
    {
        var asoExiste = await _db.Asos.NoEscopoDoTrabalhador(_db, a => a.TrabalhadorId).AnyAsync(a => a.Id == request.AsoId, ct);
        if (!asoExiste)
            throw new KeyNotFoundException($"ASO {request.AsoId} não encontrado.");

        var existente = await _db.ArquivosAso.FirstOrDefaultAsync(a => a.AsoId == request.AsoId, ct);
        if (existente is not null)
        {
            existente.NomeArquivo = request.NomeArquivo;
            existente.ContentType = request.ContentType;
            existente.Conteudo = request.Conteudo;
            existente.TamanhoBytes = request.Conteudo.LongLength;
            await _db.SaveChangesAsync(ct);
            return existente.Id;
        }

        var arquivo = new ArquivoAso
        {
            AsoId = request.AsoId,
            NomeArquivo = request.NomeArquivo,
            ContentType = request.ContentType,
            Conteudo = request.Conteudo,
            TamanhoBytes = request.Conteudo.LongLength,
        };
        _db.ArquivosAso.Add(arquivo);
        await _db.SaveChangesAsync(ct);
        return arquivo.Id;
    }
}

public record ArquivoAsoResultado(byte[] Conteudo, string ContentType, string NomeArquivo);

public record ObterArquivoAsoQuery(Guid AsoId) : IRequest<ArquivoAsoResultado?>;

public class ObterArquivoAsoQueryHandler : IRequestHandler<ObterArquivoAsoQuery, ArquivoAsoResultado?>
{
    private readonly IAppDbContext _db;

    public ObterArquivoAsoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ArquivoAsoResultado?> Handle(ObterArquivoAsoQuery request, CancellationToken ct)
    {
        if (!await _db.Asos.NoEscopoDoTrabalhador(_db, a => a.TrabalhadorId).AnyAsync(a => a.Id == request.AsoId, ct))
            return null;

        var arquivo = await _db.ArquivosAso.AsNoTracking().FirstOrDefaultAsync(a => a.AsoId == request.AsoId, ct);
        if (arquivo is null) return null;

        return new ArquivoAsoResultado(arquivo.Conteudo,
            string.IsNullOrEmpty(arquivo.ContentType) ? "application/octet-stream" : arquivo.ContentType,
            arquivo.NomeArquivo);
    }
}
