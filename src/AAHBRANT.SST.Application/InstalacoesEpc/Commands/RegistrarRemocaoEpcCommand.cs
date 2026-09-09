using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.InstalacoesEpc.Commands;

// Remoção/desinstalação — mesmo princípio da devolução de EntregaEpi: repõe o estoque da Obra.
public record RegistrarRemocaoEpcCommand(
    Guid InstalacaoEpcId,
    DateTime DataRemocao,
    string? Observacoes) : IRequest;

public class RegistrarRemocaoEpcCommandValidator : AbstractValidator<RegistrarRemocaoEpcCommand>
{
    public RegistrarRemocaoEpcCommandValidator()
    {
        RuleFor(x => x.InstalacaoEpcId).NotEmpty();
        RuleFor(x => x.DataRemocao).NotEmpty();
    }
}

public class RegistrarRemocaoEpcCommandHandler : IRequestHandler<RegistrarRemocaoEpcCommand>
{
    private readonly IAppDbContext _db;
    public RegistrarRemocaoEpcCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RegistrarRemocaoEpcCommand request, CancellationToken ct)
    {
        var instalacao = await _db.InstalacoesEpc.FirstOrDefaultAsync(x => x.Id == request.InstalacaoEpcId, ct)
            ?? throw new KeyNotFoundException("Instalação de EPC não encontrada.");

        if (instalacao.DataRemocao is null)
        {
            var estoque = await _db.EstoquesEpc
                .FirstOrDefaultAsync(x => x.CatalogoEpcId == instalacao.CatalogoEpcId && x.ObraId == instalacao.ObraId, ct);
            if (estoque is null)
            {
                estoque = new EstoqueEpc { CatalogoEpcId = instalacao.CatalogoEpcId, ObraId = instalacao.ObraId, Saldo = 0 };
                _db.EstoquesEpc.Add(estoque);
            }

            estoque.Saldo += instalacao.Quantidade;
            _db.MovimentacoesEstoqueEpc.Add(new MovimentacaoEstoqueEpc
            {
                EstoqueEpcId = estoque.Id,
                Tipo = TipoMovimentacaoEstoqueEpc.RetornoRemocao,
                Quantidade = instalacao.Quantidade,
                SaldoResultante = estoque.Saldo,
                InstalacaoEpcId = instalacao.Id,
            });
        }

        instalacao.DataRemocao = request.DataRemocao;
        instalacao.Observacoes = request.Observacoes;

        await _db.SaveChangesAsync(ct);
    }
}
