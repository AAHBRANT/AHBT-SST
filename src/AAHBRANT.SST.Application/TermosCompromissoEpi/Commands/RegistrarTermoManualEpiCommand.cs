using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.TermosCompromissoEpi.Commands;

// Registra que o funcionário JÁ assinou o termo em papel (decisão do usuário, 03/10): o sistema não
// pode fabricar assinatura eletrônica de quem assinou à mão. Quem lança assume a declaração, então
// guarda-se quem registrou e quando; a foto/PDF do papel é opcional.
public record RegistrarTermoManualEpiCommand(
    Guid TrabalhadorId,
    DateTime DataAssinaturaPapel,
    string? Observacao,
    Guid UsuarioId,
    string? NomeArquivo = null,
    byte[]? Conteudo = null,
    string? ContentType = null) : IRequest<Guid>;

public class RegistrarTermoManualEpiCommandValidator : AbstractValidator<RegistrarTermoManualEpiCommand>
{
    private static readonly string[] TiposPermitidos = { "application/pdf", "image/jpeg", "image/png" };
    private const int TamanhoMaximoBytes = 10 * 1024 * 1024;

    public RegistrarTermoManualEpiCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.UsuarioId).NotEmpty();
        RuleFor(x => x.Observacao).MaximumLength(500);

        // Hoje em Brasília (UTC-3): o técnico registra o papel de hoje à noite sem esbarrar no UTC.
        RuleFor(x => x.DataAssinaturaPapel)
            .Must(d => d.Date <= DateTime.UtcNow.AddHours(-3).Date)
            .WithMessage("A data da assinatura em papel não pode ser futura.")
            .Must(d => d.Year >= 1990)
            .WithMessage("Informe a data em que o funcionário assinou o papel.");

        When(x => x.Conteudo is { Length: > 0 }, () =>
        {
            RuleFor(x => x.NomeArquivo).NotEmpty().MaximumLength(260);
            RuleFor(x => x.ContentType)
                .Must(t => t is not null && TiposPermitidos.Contains(t))
                .WithMessage("O termo deve ser um arquivo PDF, JPEG ou PNG.");
            RuleFor(x => x.Conteudo!)
                .Must(c => c.Length <= TamanhoMaximoBytes).WithMessage("O arquivo deve ter no máximo 10 MB.")
                .Must((comando, conteudo) => ValidadorAssinaturaArquivo.AssinaturaConfere(conteudo, comando.ContentType ?? string.Empty))
                    .WithMessage("O conteúdo do arquivo não corresponde ao tipo declarado.");
        });
    }
}

public class RegistrarTermoManualEpiCommandHandler : IRequestHandler<RegistrarTermoManualEpiCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public RegistrarTermoManualEpiCommandHandler(IAppDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task<Guid> Handle(RegistrarTermoManualEpiCommand request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == request.TrabalhadorId, ct)
            ?? throw new KeyNotFoundException("Funcionário não encontrado.");

        var situacao = await TermoCompromissoEpiConsulta.ObterAsync(_db, request.TrabalhadorId, ct);
        if (situacao.Situacao == SituacaoTermoCompromissoEpi.Digital)
            throw new InvalidOperationException("Este funcionário já assinou o termo digitalmente — não é possível registrar assinatura em papel por cima.");
        if (situacao.Situacao == SituacaoTermoCompromissoEpi.Manual)
            throw new InvalidOperationException("Este funcionário já tem o termo registrado como assinado manualmente.");

        var temArquivo = request.Conteudo is { Length: > 0 };
        var termo = new TermoCompromissoEpiManual
        {
            TrabalhadorId = trabalhador.Id,
            DataAssinaturaPapel = request.DataAssinaturaPapel.Date,
            Observacao = string.IsNullOrWhiteSpace(request.Observacao) ? null : request.Observacao.Trim(),
            RegistradoPorUsuarioId = request.UsuarioId,
            ArquivoNome = temArquivo ? request.NomeArquivo : null,
            ArquivoContentType = temArquivo ? request.ContentType : null,
            ArquivoConteudo = temArquivo ? request.Conteudo : null,
            ArquivoTamanhoBytes = temArquivo ? request.Conteudo!.LongLength : null,
        };
        _db.TermosCompromissoEpiManual.Add(termo);

        await _auditoria.RegistrarAsync(
            "TermoCompromissoEpi.RegistradoManualmente",
            TermoCompromissoEpiConsulta.EntidadeTipo,
            trabalhador.Id,
            usuarioId: request.UsuarioId,
            trabalhadorId: trabalhador.Id,
            dadosDepois: new { termo.DataAssinaturaPapel, TemArquivo = temArquivo, termo.Observacao },
            ct);

        await _db.SaveChangesAsync(ct);
        return termo.Id;
    }
}
