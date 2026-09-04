using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SessoesTreinamento.Commands;

// Evidência fotográfica da turma (pedido do usuário: mínimo 3 fotos) — mesmo padrão de
// AnexarFotoEvidenciaDdsCommand, obrigatórias para encerrar (ver EncerrarSessaoTreinamentoCommand).
public record AnexarFotoEvidenciaSessaoTreinamentoCommand(
    Guid SessaoTreinamentoId,
    byte[] FotoConteudo,
    string FotoContentType) : IRequest<Guid>;

public class AnexarFotoEvidenciaSessaoTreinamentoCommandValidator : AbstractValidator<AnexarFotoEvidenciaSessaoTreinamentoCommand>
{
    private static readonly string[] TiposPermitidos = { "image/jpeg", "image/png" };
    private const int TamanhoMaximoBytes = 5 * 1024 * 1024;

    public AnexarFotoEvidenciaSessaoTreinamentoCommandValidator()
    {
        RuleFor(x => x.SessaoTreinamentoId).NotEmpty();
        RuleFor(x => x.FotoConteudo)
            .NotEmpty().WithMessage("A foto é obrigatória.")
            .Must(f => f.Length <= TamanhoMaximoBytes).WithMessage("A foto deve ter no máximo 5 MB.")
            .Must((comando, conteudo) => ValidadorAssinaturaArquivo.AssinaturaConfere(conteudo, comando.FotoContentType))
                .WithMessage("O conteúdo do arquivo não corresponde ao tipo declarado.");
        RuleFor(x => x.FotoContentType)
            .Must(t => TiposPermitidos.Contains(t)).WithMessage("A foto deve ser um arquivo JPEG ou PNG.");
    }
}

public class AnexarFotoEvidenciaSessaoTreinamentoCommandHandler : IRequestHandler<AnexarFotoEvidenciaSessaoTreinamentoCommand, Guid>
{
    private const int TotalFotosMaximo = 3;
    private readonly IAppDbContext _db;

    public AnexarFotoEvidenciaSessaoTreinamentoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(AnexarFotoEvidenciaSessaoTreinamentoCommand request, CancellationToken ct)
    {
        var sessaoExiste = await _db.SessoesTreinamento.AnyAsync(s => s.Id == request.SessaoTreinamentoId, ct);
        if (!sessaoExiste)
            throw new KeyNotFoundException($"Turma de treinamento {request.SessaoTreinamentoId} não encontrada.");

        var totalAtual = await _db.FotosEvidenciaSessaoTreinamento.CountAsync(f => f.SessaoTreinamentoId == request.SessaoTreinamentoId && f.Ativo, ct);
        if (totalAtual >= TotalFotosMaximo)
            throw new InvalidOperationException($"Esta turma já tem as {TotalFotosMaximo} fotos de evidência obrigatórias.");

        var foto = new FotoEvidenciaSessaoTreinamento
        {
            SessaoTreinamentoId = request.SessaoTreinamentoId,
            Ordem = totalAtual + 1,
            FotoConteudo = request.FotoConteudo,
            FotoContentType = request.FotoContentType,
        };
        _db.FotosEvidenciaSessaoTreinamento.Add(foto);
        await _db.SaveChangesAsync(ct);
        return foto.Id;
    }
}
