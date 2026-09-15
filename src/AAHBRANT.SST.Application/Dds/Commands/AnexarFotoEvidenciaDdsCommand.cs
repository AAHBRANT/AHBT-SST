using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Commands;

// Evidência fotográfica do registro diário (31/08) — até 3 fotos por Dds, obrigatórias para
// encerrar (ver EncerrarDdsCommand). Distinta da foto de presença por participante.
//
// Slot fixo por Ordem (04/09, pedido do usuário — grade de 3 quadros individuais, cada um
// substituível): antes o Ordem era sempre "a próxima posição livre" (fila); agora o cliente escolhe
// EM QUAL dos 3 quadros está anexando, e reanexar no mesmo quadro SUBSTITUI o conteúdo em vez de
// criar um novo registro — evita acumular linhas soft-deletadas a cada troca de foto.
public record AnexarFotoEvidenciaDdsCommand(
    Guid DdsId,
    int Ordem,
    byte[] FotoConteudo,
    string FotoContentType) : IRequest<Guid>;

public class AnexarFotoEvidenciaDdsCommandValidator : AbstractValidator<AnexarFotoEvidenciaDdsCommand>
{
    private static readonly string[] TiposPermitidos = { "image/jpeg", "image/png" };
    private const int TamanhoMaximoBytes = 5 * 1024 * 1024;
    private const int TotalFotosMaximo = 3;

    public AnexarFotoEvidenciaDdsCommandValidator()
    {
        RuleFor(x => x.DdsId).NotEmpty();
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

public class AnexarFotoEvidenciaDdsCommandHandler : IRequestHandler<AnexarFotoEvidenciaDdsCommand, Guid>
{
    private readonly IAppDbContext _db;

    public AnexarFotoEvidenciaDdsCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(AnexarFotoEvidenciaDdsCommand request, CancellationToken ct)
    {
        var ddsExiste = await _db.Dds.AnyAsync(d => d.Id == request.DdsId, ct);
        if (!ddsExiste)
            throw new KeyNotFoundException($"DDS {request.DdsId} não encontrado.");

        var fotoExistente = await _db.DdsFotosEvidencia
            .FirstOrDefaultAsync(f => f.DdsId == request.DdsId && f.Ordem == request.Ordem && f.Ativo, ct);

        if (fotoExistente is not null)
        {
            fotoExistente.FotoConteudo = request.FotoConteudo;
            fotoExistente.FotoContentType = request.FotoContentType;
            await _db.SaveChangesAsync(ct);
            return fotoExistente.Id;
        }

        var foto = new DdsFotoEvidencia
        {
            DdsId = request.DdsId,
            Ordem = request.Ordem,
            FotoConteudo = request.FotoConteudo,
            FotoContentType = request.FotoContentType,
        };
        _db.DdsFotosEvidencia.Add(foto);
        await _db.SaveChangesAsync(ct);
        return foto.Id;
    }
}
