using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Commands;

// Evidência de presença passou a ser exclusivamente biométrica (2026-08-31, pedido do usuário) —
// reaproveita IAutenticacaoBiometriaLocalService (mesmo serviço do Motor de Assinatura Eletrônica):
// o match 1:N já aconteceu no agente local (leitor Futronic FS80H), aqui só se reautentica o
// dispositivo e se confere o score contra o limiar configurado antes de gravar a presença.
public record RegistrarParticipanteCommand(
    Guid DdsId,
    Guid TrabalhadorId,
    Guid DispositivoId,
    string SegredoDispositivo,
    double Score) : IRequest<Guid>;

public class RegistrarParticipanteCommandValidator : AbstractValidator<RegistrarParticipanteCommand>
{
    public RegistrarParticipanteCommandValidator()
    {
        RuleFor(x => x.DdsId).NotEmpty();
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.DispositivoId).NotEmpty();
        RuleFor(x => x.SegredoDispositivo).NotEmpty();
        RuleFor(x => x.Score).InclusiveBetween(0, 100);
    }
}

public class RegistrarParticipanteCommandHandler : IRequestHandler<RegistrarParticipanteCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IAutenticacaoBiometriaLocalService _autenticacao;

    public RegistrarParticipanteCommandHandler(IAppDbContext db, IAutenticacaoBiometriaLocalService autenticacao)
    {
        _db = db;
        _autenticacao = autenticacao;
    }

    public async Task<Guid> Handle(RegistrarParticipanteCommand request, CancellationToken ct)
    {
        var ddsExiste = await _db.Dds.AnyAsync(d => d.Id == request.DdsId, ct);
        if (!ddsExiste)
            throw new KeyNotFoundException($"DDS {request.DdsId} não encontrado.");

        var jaParticipa = await _db.DdsParticipantes
            .AnyAsync(p => p.DdsId == request.DdsId && p.TrabalhadorId == request.TrabalhadorId, ct);
        if (jaParticipa)
            throw new InvalidOperationException("Este trabalhador já está registrado como participante deste DDS.");

        var resultado = await _autenticacao.AutenticarPorMatchLocalAsync(
            request.DispositivoId, request.SegredoDispositivo, request.TrabalhadorId, request.Score, ct);

        var participante = new Domain.Entidades.DdsParticipante
        {
            DdsId = request.DdsId,
            TrabalhadorId = resultado.TrabalhadorId,
            FotoTipo = TipoFotoParticipante.Biometria,
            ScoreConfianca = request.Score,
        };
        _db.DdsParticipantes.Add(participante);
        await _db.SaveChangesAsync(ct);
        return participante.Id;
    }
}
