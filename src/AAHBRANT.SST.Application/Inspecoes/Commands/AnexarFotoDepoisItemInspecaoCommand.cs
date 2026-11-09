using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Commands;

// Evidência posterior (depois de resolvido o achado) — par de AnexarFotoItemInspecaoCommand (a
// evidência anterior), pedido "Patrulha de Segurança do Trabalho" (planilha do usuário, 31/08).
public record AnexarFotoDepoisItemInspecaoCommand(
    Guid RespostaId,
    byte[] FotoConteudo,
    string FotoContentType, string? Metadados = null, string? VersaoEsperada = null) : IRequest<string?>;

public class AnexarFotoDepoisItemInspecaoCommandValidator : AbstractValidator<AnexarFotoDepoisItemInspecaoCommand>
{
    private static readonly string[] TiposPermitidos = { "image/jpeg", "image/png" };
    private const int TamanhoMaximoBytes = 5 * 1024 * 1024;

    public AnexarFotoDepoisItemInspecaoCommandValidator()
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

public class AnexarFotoDepoisItemInspecaoCommandHandler : IRequestHandler<AnexarFotoDepoisItemInspecaoCommand, string?>
{
    private readonly IAppDbContext _db;

    public AnexarFotoDepoisItemInspecaoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<string?> Handle(AnexarFotoDepoisItemInspecaoCommand request, CancellationToken ct)
    {
        var resposta = await _db.InspecaoItemRespostas.Include(r => r.Inspecao).FirstOrDefaultAsync(r => r.Id == request.RespostaId, ct)
            ?? throw new KeyNotFoundException($"Resposta {request.RespostaId} não encontrada.");
        await ProtecaoDocumentoAssinado.GarantirEditavelAsync(_db, "Inspecao", resposta.InspecaoId, ct);
        if (request.VersaoEsperada is not null && request.VersaoEsperada != Convert.ToBase64String(resposta.RowVersion ?? []))
            throw new DbUpdateConcurrencyException("O item foi alterado enquanto o dispositivo estava offline.");

        if (resposta.Inspecao!.Status != AAHBRANT.SST.Domain.Enums.StatusInspecao.EmAndamento)
            throw new InvalidOperationException("A inspeção já foi finalizada.");
        resposta.FotoDepoisMetadadosJson = await DadosCapturaFoto.PrepararAsync(request.Metadados, resposta.Id, resposta.Inspecao.ObraId, _db, ct);
        resposta.FotoDepoisConteudo = request.FotoConteudo;
        resposta.FotoDepoisContentType = request.FotoContentType;
        await _db.SaveChangesAsync(ct);
        return resposta.RowVersion is null ? null : Convert.ToBase64String(resposta.RowVersion);
    }
}
