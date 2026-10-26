using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Commands;

public record AnexarFotoItemInspecaoCommand(
    Guid RespostaId,
    byte[] FotoConteudo,
    string FotoContentType, string? Metadados = null, string? VersaoEsperada = null) : IRequest<string?>;

public class AnexarFotoItemInspecaoCommandValidator : AbstractValidator<AnexarFotoItemInspecaoCommand>
{
    private static readonly string[] TiposPermitidos = { "image/jpeg", "image/png" };
    private const int TamanhoMaximoBytes = 5 * 1024 * 1024;

    public AnexarFotoItemInspecaoCommandValidator()
    {
        RuleFor(x => x.RespostaId).NotEmpty();
        RuleFor(x => x.FotoConteudo)
            .NotEmpty().WithMessage("A foto é obrigatória.")
            .Must(f => f.Length <= TamanhoMaximoBytes).WithMessage("A foto deve ter no máximo 5 MB.")
            .Must((comando, conteudo) => ValidadorAssinaturaArquivo.AssinaturaConfere(conteudo, comando.FotoContentType))
                .WithMessage("O conteúdo do arquivo não corresponde ao tipo declarado.");
        RuleFor(x => x.FotoContentType)
            .Must(t => TiposPermitidos.Contains(t)).WithMessage("A foto deve ser um arquivo JPEG ou PNG.");
    }
}

public class AnexarFotoItemInspecaoCommandHandler : IRequestHandler<AnexarFotoItemInspecaoCommand, string?>
{
    private readonly IAppDbContext _db;

    public AnexarFotoItemInspecaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<string?> Handle(AnexarFotoItemInspecaoCommand request, CancellationToken ct)
    {
        var resposta = await _db.InspecaoItemRespostas.Include(r => r.Inspecao).FirstOrDefaultAsync(r => r.Id == request.RespostaId, ct)
            ?? throw new KeyNotFoundException($"Resposta {request.RespostaId} não encontrada.");
        await ProtecaoDocumentoAssinado.GarantirEditavelAsync(_db, "Inspecao", resposta.InspecaoId, ct);
        if (request.VersaoEsperada is not null && request.VersaoEsperada != Convert.ToBase64String(resposta.RowVersion ?? []))
            throw new DbUpdateConcurrencyException("O item foi alterado enquanto o dispositivo estava offline.");

        resposta.FotoConteudo = request.FotoConteudo;
        resposta.FotoContentType = request.FotoContentType;

        if (resposta.Inspecao!.Status == AAHBRANT.SST.Domain.Enums.StatusInspecao.Concluida)
            throw new InvalidOperationException("A inspeção já foi finalizada.");
        resposta.FotoMetadadosJson = await DadosCapturaFoto.PrepararAsync(request.Metadados, resposta.Inspecao.ObraId, _db, ct);
        await _db.SaveChangesAsync(ct);
        return resposta.RowVersion is null ? null : Convert.ToBase64String(resposta.RowVersion);
    }
}
