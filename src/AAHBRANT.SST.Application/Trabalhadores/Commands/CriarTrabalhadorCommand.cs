using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Trabalhadores.Commands;

public record CriarTrabalhadorCommand(
    Guid ObraId,
    Guid? SetorId,
    Guid? EquipeId,
    Guid FuncaoId,
    string Nome,
    string Matricula,
    string Cpf,
    TipoVinculo Vinculo,
    DateTime DataAdmissao,
    string? Turno) : IRequest<Guid>;

public class CriarTrabalhadorCommandValidator : AbstractValidator<CriarTrabalhadorCommand>
{
    public CriarTrabalhadorCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.FuncaoId).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Matricula).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Cpf).NotEmpty().Length(11).Matches("^[0-9]+$")
            .Must(CpfValidador.EhValido).WithMessage("CPF inválido.");
        RuleFor(x => x.DataAdmissao).NotEmpty();
    }
}

public class CriarTrabalhadorCommandHandler : IRequestHandler<CriarTrabalhadorCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarTrabalhadorCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarTrabalhadorCommand request, CancellationToken ct)
    {
        var trabalhador = new Trabalhador
        {
            ObraId = request.ObraId,
            SetorId = request.SetorId,
            EquipeId = request.EquipeId,
            FuncaoId = request.FuncaoId,
            Nome = request.Nome,
            Matricula = request.Matricula,
            Cpf = request.Cpf,
            Vinculo = request.Vinculo,
            DataAdmissao = request.DataAdmissao,
            Turno = request.Turno
        };

        _db.Trabalhadores.Add(trabalhador);
        await _db.SaveChangesAsync(ct);
        return trabalhador.Id;
    }
}
