using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SessoesTreinamento.Commands;

// Evidência fotográfica da turma (pedido do usuário: mínimo 3 fotos) — mesmo padrão de
// AnexarFotoEvidenciaDdsCommand, obrigatórias para encerrar (ver EncerrarSessaoTreinamentoCommand).
//
// Slot fixo por Ordem (04/09, pedido do usuário — grade de 3 quadros individuais, cada um
// substituível): reanexar no mesmo quadro SUBSTITUI o conteúdo em vez de criar um novo registro.
public record AnexarFotoEvidenciaSessaoTreinamentoCommand(
    Guid SessaoTreinamentoId,
    int Ordem,
    byte[] FotoConteudo,
    string FotoContentType, string? Metadados = null) : IRequest<Guid>;

public class AnexarFotoEvidenciaSessaoTreinamentoCommandValidator : AbstractValidator<AnexarFotoEvidenciaSessaoTreinamentoCommand>
{
    private static readonly string[] TiposPermitidos = { "image/jpeg", "image/png" };
    private const int TamanhoMaximoBytes = 5 * 1024 * 1024;
    private const int TotalFotosMaximo = 3;

    public AnexarFotoEvidenciaSessaoTreinamentoCommandValidator()
    {
        RuleFor(x => x.SessaoTreinamentoId).NotEmpty();
        RuleFor(x => x.Ordem).InclusiveBetween(1, TotalFotosMaximo);
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
    private readonly IAppDbContext _db;

    public AnexarFotoEvidenciaSessaoTreinamentoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(AnexarFotoEvidenciaSessaoTreinamentoCommand request, CancellationToken ct)
    {
        var registro = await _db.SessoesTreinamento.FirstOrDefaultAsync(x => x.Id == request.SessaoTreinamentoId, ct)
            ?? throw new KeyNotFoundException("Registro não encontrado.");
        if (registro.Status == AAHBRANT.SST.Domain.Enums.StatusSessaoTreinamento.Concluida)
            throw new InvalidOperationException("O registro já foi finalizado. As fotos não podem ser alteradas.");
        var metadados = await DadosCapturaFoto.PrepararAsync(request.Metadados, registro.ObraId, _db, ct);

        var fotoExistente = await _db.FotosEvidenciaSessaoTreinamento
            .FirstOrDefaultAsync(f => f.SessaoTreinamentoId == request.SessaoTreinamentoId && f.Ordem == request.Ordem && f.Ativo, ct);

        if (fotoExistente is not null) fotoExistente.Ativo = false;

        var foto = new FotoEvidenciaSessaoTreinamento
        {
            SessaoTreinamentoId = request.SessaoTreinamentoId,
            Ordem = request.Ordem,
            FotoConteudo = request.FotoConteudo,
            FotoContentType = request.FotoContentType,
            FotoMetadadosJson = metadados,
        };
        _db.FotosEvidenciaSessaoTreinamento.Add(foto);
        await _db.SaveChangesAsync(ct);
        return foto.Id;
    }
}
