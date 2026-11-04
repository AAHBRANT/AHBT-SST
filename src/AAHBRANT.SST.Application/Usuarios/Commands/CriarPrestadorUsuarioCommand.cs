using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Usuarios.Commands;

// Usuário que não é funcionário (engenheiro ou instrutor PJ, 04/11/2026): o motor de assinatura e o instrutor da turma
// sempre apontam para um Trabalhador, então o prestador ganha um cadastro de pessoa com Vinculo = PrestadorServico,
// fora do efetivo da obra, e o usuário já fica vinculado a ele. A obra é só a base do cadastro (o campo é obrigatório);
// o alcance do usuário continua sendo o dos perfis por obra. Não abre plano de integração nem kit de EPI de admissão.
public record CriarPrestadorUsuarioCommand(Guid UsuarioId, string Cpf, Guid ObraId, Guid FuncaoId) : IRequest<Guid>;

public class CriarPrestadorUsuarioCommandValidator : AbstractValidator<CriarPrestadorUsuarioCommand>
{
    public CriarPrestadorUsuarioCommandValidator()
    {
        RuleFor(x => x.UsuarioId).NotEmpty();
        RuleFor(x => x.Cpf).NotEmpty().Length(11).Matches("^[0-9]+$").WithMessage("Informe o CPF com 11 dígitos.");
        RuleFor(x => x.ObraId).NotEmpty().WithMessage("Escolha a obra base do prestador.");
        RuleFor(x => x.FuncaoId).NotEmpty().WithMessage("Escolha a função do prestador.");
    }
}

public class CriarPrestadorUsuarioCommandHandler : IRequestHandler<CriarPrestadorUsuarioCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarPrestadorUsuarioCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarPrestadorUsuarioCommand request, CancellationToken ct)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == request.UsuarioId, ct)
            ?? throw new KeyNotFoundException("Usuário não encontrado.");
        if (usuario.TrabalhadorId is not null)
            throw new InvalidOperationException("Este usuário já está vinculado a um cadastro. Troque para \"Nenhum\" antes de cadastrar como prestador.");

        var prestador = new Trabalhador
        {
            ObraId = request.ObraId,
            FuncaoId = request.FuncaoId,
            Nome = usuario.Nome,
            Cpf = request.Cpf,
            Vinculo = TipoVinculo.PrestadorServico,
            DataAdmissao = DateTime.UtcNow.Date,
        };
        _db.Trabalhadores.Add(prestador);
        usuario.Trabalhador = prestador;
        await TratamentoCpfDuplicado.SalvarAsync(_db, ct);
        return prestador.Id;
    }
}
