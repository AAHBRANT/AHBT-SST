using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.PlanosIntegracao;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
    private readonly IAutenticacaoFacialService _facial;
    private readonly ILogger<AtualizarTrabalhadorCommandHandler> _logger;
    private readonly IPlanoIntegracaoService? _planoIntegracao;

    public AtualizarTrabalhadorCommandHandler(IAppDbContext db, IAutenticacaoFacialService facial, ILogger<AtualizarTrabalhadorCommandHandler> logger,
        IPlanoIntegracaoService? planoIntegracao = null)
    {
        _db = db;
        _facial = facial;
        _logger = logger;
        _planoIntegracao = planoIntegracao;
    }

    public async Task Handle(AtualizarTrabalhadorCommand request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Trabalhador {request.Id} não encontrado.");

        var obraAnteriorId = trabalhador.ObraId;
        var funcaoAnteriorId = trabalhador.FuncaoId;
        var demissaoAnterior = trabalhador.DataDemissao;
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

        await TratamentoCpfDuplicado.SalvarAsync(_db, ct);

        if (obraAnteriorId != trabalhador.ObraId && trabalhador.AzureFacePersonId is not null)
            await RealocacaoCadastroFacial.TentarAsync(_facial, _logger, trabalhador.Id, obraAnteriorId, ct);

        if (_planoIntegracao is not null)
        {
            MotivoPlanoIntegracao? motivo = funcaoAnteriorId != trabalhador.FuncaoId ? MotivoPlanoIntegracao.TrocaFuncao
                : obraAnteriorId != trabalhador.ObraId ? MotivoPlanoIntegracao.TransferenciaObra
                : null;
            if (motivo is not null || demissaoAnterior != trabalhador.DataDemissao)
                await _planoIntegracao.SincronizarAsync(trabalhador.Id, motivo, ct);
        }
    }
}
