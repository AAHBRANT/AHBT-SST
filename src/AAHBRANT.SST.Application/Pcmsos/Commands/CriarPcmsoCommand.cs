using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.Pcmsos.Commands;

public record CriarPcmsoCommand(
    string Nome,
    string? Versao,
    DateTime? Validade,
    DateTime DataEmissao,
    Guid? ResponsavelUsuarioId,
    Guid? ObraId,
    Guid? SetorId,
    string? MedicoResponsavelNome,
    string? MedicoResponsavelCrm,
    string? FuncoesContempladas,
    string? RiscosConsiderados,
    string? ExamesPrevistos,
    string? Periodicidades,
    string? UnidadesObrasAbrangidas) : IRequest<Guid>;

public class CriarPcmsoCommandValidator : AbstractValidator<CriarPcmsoCommand>
{
    public CriarPcmsoCommandValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Versao).MaximumLength(50);
        RuleFor(x => x.MedicoResponsavelNome).MaximumLength(150);
        RuleFor(x => x.MedicoResponsavelCrm).MaximumLength(30);
        RuleFor(x => x.DataEmissao).NotEmpty();
    }
}

public class CriarPcmsoCommandHandler : IRequestHandler<CriarPcmsoCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IGeradorNumeroDocumentoService _geradorNumero;

    public CriarPcmsoCommandHandler(IAppDbContext db, IGeradorNumeroDocumentoService geradorNumero)
    {
        _db = db;
        _geradorNumero = geradorNumero;
    }

    public async Task<Guid> Handle(CriarPcmsoCommand request, CancellationToken ct)
    {
        // Usuário restrito só grava PCMSO numa obra dele. "Nenhuma obra" (ObraId nulo) só aparece para
        // acesso global (filtro do SstDbContext), então o restrito perderia o próprio registro.
        if (!_db.ObraNoEscopo(request.ObraId))
            throw new KeyNotFoundException("Obra não encontrada. Selecione uma obra à qual você tem acesso.");

        var pcmso = new PcmsoDetalhe
        {
            NumeroDocumento = await _geradorNumero.GerarAsync("PCMSO", ct),
            Nome = request.Nome,
            Versao = request.Versao,
            Validade = request.Validade,
            DataEmissao = request.DataEmissao,
            ResponsavelUsuarioId = request.ResponsavelUsuarioId,
            ObraId = request.ObraId,
            SetorId = request.SetorId,
            Status = StatusPcmsoDocumento.Rascunho,
            MedicoResponsavelNome = request.MedicoResponsavelNome,
            MedicoResponsavelCrm = request.MedicoResponsavelCrm,
            FuncoesContempladas = request.FuncoesContempladas,
            RiscosConsiderados = request.RiscosConsiderados,
            ExamesPrevistos = request.ExamesPrevistos,
            Periodicidades = request.Periodicidades,
            UnidadesObrasAbrangidas = request.UnidadesObrasAbrangidas
        };

        _db.PcmsoDetalhes.Add(pcmso);
        await _db.SaveChangesAsync(ct);
        return pcmso.Id;
    }
}
