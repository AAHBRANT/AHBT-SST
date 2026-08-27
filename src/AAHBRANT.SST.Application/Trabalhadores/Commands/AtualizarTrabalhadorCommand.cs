using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Commands;

public record AtualizarTrabalhadorCommand(
    Guid Id,
    Guid ObraId,
    Guid? SetorId,
    Guid? EquipeId,
    Guid FuncaoId,
    string Nome,
    string Matricula,
    string Cpf,
    TipoVinculo Vinculo,
    DateTime DataAdmissao,
    DateTime? DataDemissao,
    string? Turno) : IRequest;

public class AtualizarTrabalhadorCommandValidator : AbstractValidator<AtualizarTrabalhadorCommand>
{
    public AtualizarTrabalhadorCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.FuncaoId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Matricula).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Cpf).NotEmpty().Length(11).Matches("^[0-9]+$")
            .Must(CpfValidador.EhValido).WithMessage("CPF inválido.");
        RuleFor(x => x.DataAdmissao).NotEmpty();
    }
}

public class AtualizarTrabalhadorCommandHandler : IRequestHandler<AtualizarTrabalhadorCommand>
{
    private readonly IAppDbContext _db;

    public AtualizarTrabalhadorCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AtualizarTrabalhadorCommand request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Trabalhador {request.Id} não encontrado.");

        trabalhador.ObraId = request.ObraId;
        trabalhador.SetorId = request.SetorId;
        trabalhador.EquipeId = request.EquipeId;
        trabalhador.FuncaoId = request.FuncaoId;
        trabalhador.Nome = request.Nome;
        trabalhador.Matricula = request.Matricula;
        trabalhador.Cpf = request.Cpf;
        trabalhador.Vinculo = request.Vinculo;
        trabalhador.DataAdmissao = request.DataAdmissao;
        trabalhador.DataDemissao = request.DataDemissao;
        trabalhador.Turno = request.Turno;

        await _db.SaveChangesAsync(ct);
    }
}
