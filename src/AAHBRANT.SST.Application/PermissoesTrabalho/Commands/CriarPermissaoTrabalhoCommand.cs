using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissoesTrabalho.Commands;

// A PT sempre nasce em elaboração ("autorização" é uma etapa distinta do cadastro, mesmo padrão de
// CriarAprCommand). Nasce já com os 6 PreRequisitos (§2) e os 15 Verificacoes (§4) do formulário —
// todos "em branco" (Atendido=false / Resposta=null) — mesmo princípio de CriarInspecaoCommand
// gerando uma InspecaoItemResposta em branco por item do checklist.
public record CriarPermissaoTrabalhoCommand(
    Guid AtividadeId,
    string DescricaoAtividade,
    string Local,
    string? EmpresaExecutante,
    Guid? EquipeId,
    DateTime Data,
    TimeSpan? HorarioInicio,
    TimeSpan? HorarioFim,
    DateTime? Validade,
    Guid? ResponsavelExecucaoUsuarioId,
    Guid? ResponsavelAreaUsuarioId,
    List<Guid> ResponsaveisIds) : IRequest<Guid>;

public class CriarPermissaoTrabalhoCommandValidator : AbstractValidator<CriarPermissaoTrabalhoCommand>
{
    public CriarPermissaoTrabalhoCommandValidator()
    {
        RuleFor(x => x.AtividadeId).NotEmpty();
        RuleFor(x => x.DescricaoAtividade).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Local).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EmpresaExecutante).MaximumLength(200);
    }
}

public class CriarPermissaoTrabalhoCommandHandler : IRequestHandler<CriarPermissaoTrabalhoCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IGeradorNumeroDocumentoService _geradorNumero;

    public CriarPermissaoTrabalhoCommandHandler(IAppDbContext db, IGeradorNumeroDocumentoService geradorNumero)
    {
        _db = db;
        _geradorNumero = geradorNumero;
    }

    public async Task<Guid> Handle(CriarPermissaoTrabalhoCommand request, CancellationToken ct)
    {
        var atividadeExiste = await _db.Atividades.AnyAsync(a => a.Id == request.AtividadeId, ct);
        if (!atividadeExiste)
            throw new KeyNotFoundException($"Atividade {request.AtividadeId} não encontrada.");

        var pt = new PermissaoTrabalho
        {
            NumeroPt = await _geradorNumero.GerarAsync("PT", ct),
            AtividadeId = request.AtividadeId,
            DescricaoAtividade = request.DescricaoAtividade,
            Local = request.Local,
            EmpresaExecutante = request.EmpresaExecutante,
            EquipeId = request.EquipeId,
            Data = request.Data,
            HorarioInicio = request.HorarioInicio,
            HorarioFim = request.HorarioFim,
            Validade = request.Validade,
            ResponsavelExecucaoUsuarioId = request.ResponsavelExecucaoUsuarioId,
            ResponsavelAreaUsuarioId = request.ResponsavelAreaUsuarioId,
        };

        foreach (var item in Enum.GetValues<ItemPreRequisitoPt>())
            pt.PreRequisitos.Add(new PermissaoTrabalhoPreRequisito { Item = item });

        foreach (var item in Enum.GetValues<ItemVerificacaoPt>())
            pt.Verificacoes.Add(new PermissaoTrabalhoVerificacao { Item = item });

        foreach (var trabalhadorId in request.ResponsaveisIds.Distinct())
            pt.Responsaveis.Add(new PermissaoTrabalhoResponsavel { TrabalhadorId = trabalhadorId });

        _db.PermissoesTrabalho.Add(pt);
        await _db.SaveChangesAsync(ct);
        return pt.Id;
    }
}
