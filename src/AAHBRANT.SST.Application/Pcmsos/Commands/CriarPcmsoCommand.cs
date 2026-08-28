using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Pcmsos.Commands;

public record CriarPcmsoCommand(
    string Nome,
    string? Versao,
    DateTime? Validade,
    DateTime DataEmissao,
    Guid? ResponsavelUsuarioId,
    Guid? ObraId,
    Guid? SetorId,
    string? Arquivo,
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
        RuleFor(x => x.Arquivo).MaximumLength(500);
        RuleFor(x => x.MedicoResponsavelNome).MaximumLength(150);
        RuleFor(x => x.MedicoResponsavelCrm).MaximumLength(30);
        RuleFor(x => x.DataEmissao).NotEmpty();
    }
}

public class CriarPcmsoCommandHandler : IRequestHandler<CriarPcmsoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarPcmsoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarPcmsoCommand request, CancellationToken ct)
    {
        if (request.ResponsavelUsuarioId.HasValue &&
            !await _db.Usuarios.AnyAsync(u => u.Id == request.ResponsavelUsuarioId, ct))
            throw new KeyNotFoundException($"Usuário {request.ResponsavelUsuarioId} não encontrado.");

        if (request.ObraId.HasValue &&
            !await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        if (request.SetorId.HasValue &&
            !await _db.Setores.AnyAsync(s => s.Id == request.SetorId, ct))
            throw new KeyNotFoundException($"Setor {request.SetorId} não encontrado.");

        var documento = new DocumentoGestao
        {
            Nome = request.Nome,
            Tipo = "PCMSO",
            Categoria = "SST",
            ResponsavelUsuarioId = request.ResponsavelUsuarioId,
            Versao = request.Versao,
            Validade = request.Validade,
            DataEmissao = request.DataEmissao,
            ObraId = request.ObraId,
            SetorId = request.SetorId,
            Arquivo = request.Arquivo,
        };
        _db.DocumentosGestao.Add(documento);

        var detalhe = new PcmsoDetalhe
        {
            DocumentoGestao = documento,
            MedicoResponsavelNome = request.MedicoResponsavelNome,
            MedicoResponsavelCrm = request.MedicoResponsavelCrm,
            FuncoesContempladas = request.FuncoesContempladas,
            RiscosConsiderados = request.RiscosConsiderados,
            ExamesPrevistos = request.ExamesPrevistos,
            Periodicidades = request.Periodicidades,
            UnidadesObrasAbrangidas = request.UnidadesObrasAbrangidas,
        };
        _db.PcmsoDetalhes.Add(detalhe);

        await _db.SaveChangesAsync(ct);
        return documento.Id;
    }
}
