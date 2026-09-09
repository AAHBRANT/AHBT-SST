using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.InstalacoesEpc.Commands;

// Registro de EPC instalado numa Obra — equivalente da entrega de EPI, mas sem trabalhador/
// assinatura. Mesmas regras de bloqueio confirmadas para EPI: CA vencido e estoque insuficiente
// impedem o registro (não é apenas um aviso).
public record CriarInstalacaoEpcCommand(
    Guid CatalogoEpcId,
    Guid ObraId,
    string? LocalInstalacao,
    int Quantidade,
    DateTime DataInstalacao,
    DateTime? DataValidade) : IRequest<Guid>;

public class CriarInstalacaoEpcCommandValidator : AbstractValidator<CriarInstalacaoEpcCommand>
{
    public CriarInstalacaoEpcCommandValidator()
    {
        RuleFor(x => x.CatalogoEpcId).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.DataInstalacao).NotEmpty();
        RuleFor(x => x.Quantidade).GreaterThan(0);
    }
}

public class CriarInstalacaoEpcCommandHandler : IRequestHandler<CriarInstalacaoEpcCommand, Guid>
{
    private readonly IAppDbContext _db;
    public CriarInstalacaoEpcCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarInstalacaoEpcCommand request, CancellationToken ct)
    {
        var catalogo = await _db.CatalogoEpcs.FirstOrDefaultAsync(x => x.Id == request.CatalogoEpcId, ct)
            ?? throw new KeyNotFoundException("EPC de catálogo não encontrado.");

        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException("Obra não encontrada.");

        if (catalogo.CertificadoAprovacaoValidade is not null && catalogo.CertificadoAprovacaoValidade < DateTime.UtcNow)
            throw new InvalidOperationException("Este EPC está com o Certificado de Aprovação (CA) vencido — não é possível registrar a instalação.");

        var estoque = await _db.EstoquesEpc
            .FirstOrDefaultAsync(x => x.CatalogoEpcId == request.CatalogoEpcId && x.ObraId == request.ObraId, ct);
        var saldoAtual = estoque?.Saldo ?? 0;

        if (saldoAtual < request.Quantidade)
            throw new InvalidOperationException($"Estoque insuficiente para este EPC nesta obra (saldo atual: {saldoAtual}).");

        var instalacao = new InstalacaoEpc
        {
            CatalogoEpcId = request.CatalogoEpcId,
            ObraId = request.ObraId,
            LocalInstalacao = request.LocalInstalacao,
            Quantidade = request.Quantidade,
            DataInstalacao = request.DataInstalacao,
            DataValidade = request.DataValidade,
        };
        _db.InstalacoesEpc.Add(instalacao);

        estoque!.Saldo -= request.Quantidade;
        _db.MovimentacoesEstoqueEpc.Add(new MovimentacaoEstoqueEpc
        {
            EstoqueEpcId = estoque.Id,
            Tipo = TipoMovimentacaoEstoqueEpc.SaidaInstalacao,
            Quantidade = request.Quantidade,
            SaldoResultante = estoque.Saldo,
            InstalacaoEpcId = instalacao.Id,
        });

        await _db.SaveChangesAsync(ct);
        return instalacao.Id;
    }
}
