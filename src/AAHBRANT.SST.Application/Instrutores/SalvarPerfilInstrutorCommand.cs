using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Instrutores;

// Cria ou atualiza o perfil de instrutor de um trabalhador. Autorizar o uso em outras obras (digital nos
// leitores delas) é decisão de quem administra e fica registrada com data e usuário. Desligar a
// autorização limpa o registro e a digital deixa de ir para os outros leitores na próxima sincronização.
public record SalvarPerfilInstrutorCommand(
    Guid TrabalhadorId,
    FormacaoInstrutor Formacao,
    ConselhoInstrutor? Conselho,
    string? NumeroRegistro,
    string? UfRegistro,
    bool PodeAssinarEmOutrasObras,
    string? AzureAdObjectId = null) : IRequest<Guid>;

public class SalvarPerfilInstrutorCommandValidator : AbstractValidator<SalvarPerfilInstrutorCommand>
{
    public SalvarPerfilInstrutorCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.Formacao).IsInEnum();
        RuleFor(x => x.NumeroRegistro).MaximumLength(30);
        RuleFor(x => x.UfRegistro).MaximumLength(2);
    }
}

public class SalvarPerfilInstrutorCommandHandler : IRequestHandler<SalvarPerfilInstrutorCommand, Guid>
{
    private readonly IAppDbContext _db;

    public SalvarPerfilInstrutorCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(SalvarPerfilInstrutorCommand request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == request.TrabalhadorId && t.Ativo, ct)
            ?? throw new KeyNotFoundException("Trabalhador não encontrado.");

        var perfil = await _db.PerfisInstrutor.FirstOrDefaultAsync(p => p.TrabalhadorId == trabalhador.Id, ct);
        if (perfil is null)
        {
            perfil = new PerfilInstrutor { TrabalhadorId = trabalhador.Id };
            _db.PerfisInstrutor.Add(perfil);
        }

        perfil.Formacao = request.Formacao;
        perfil.Conselho = request.Conselho;
        perfil.NumeroRegistro = string.IsNullOrWhiteSpace(request.NumeroRegistro) ? null : request.NumeroRegistro.Trim();
        perfil.UfRegistro = string.IsNullOrWhiteSpace(request.UfRegistro) ? null : request.UfRegistro.Trim().ToUpperInvariant();

        if (request.PodeAssinarEmOutrasObras && !perfil.PodeAssinarEmOutrasObras)
        {
            var usuarioId = string.IsNullOrEmpty(request.AzureAdObjectId)
                ? null
                : await _db.Usuarios.Where(u => u.AzureAdObjectId == request.AzureAdObjectId).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);
            perfil.AutorizadoOutrasObrasEm = DateTime.UtcNow;
            perfil.AutorizadoOutrasObrasPorUsuarioId = usuarioId;
        }
        else if (!request.PodeAssinarEmOutrasObras)
        {
            perfil.AutorizadoOutrasObrasEm = null;
            perfil.AutorizadoOutrasObrasPorUsuarioId = null;
        }
        perfil.PodeAssinarEmOutrasObras = request.PodeAssinarEmOutrasObras;

        await _db.SaveChangesAsync(ct);
        return perfil.Id;
    }
}
