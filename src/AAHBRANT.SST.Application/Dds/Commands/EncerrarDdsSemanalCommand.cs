using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Commands;

// Fechamento real da semana (31/08, pedido do usuário: "o DDS é feito e assinado todo dia, mas só
// é finalizado de verdade no fim da semana") — bloqueia até os 5 dias úteis existirem e estarem
// individualmente encerrados (o que já exige checklist + 3 fotos + presenças em cada um, ver
// EncerrarDdsCommand). "Responsável da Obra/SST" é o usuário logado que clica em encerrar (mesmo
// raciocínio do ResponsavelUsuarioId da criação); "Responsável da Empresa Terceirizada" não tem
// login no sistema — texto livre, só quando Tipo = Terceirizados, igual ao papel.
public record EncerrarDdsSemanalCommand(
    Guid Id,
    string? AzureAdObjectId,
    string? ResponsavelEmpresaTerceirizadaNome,
    string? ResponsavelEmpresaTerceirizadaFuncao) : IRequest;

public class EncerrarDdsSemanalCommandValidator : AbstractValidator<EncerrarDdsSemanalCommand>
{
    public EncerrarDdsSemanalCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class EncerrarDdsSemanalCommandHandler : IRequestHandler<EncerrarDdsSemanalCommand>
{
    private readonly IAppDbContext _db;

    public EncerrarDdsSemanalCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(EncerrarDdsSemanalCommand request, CancellationToken ct)
    {
        var semanal = await _db.DdsSemanais
            .Include(s => s.RegistrosDiarios)
            .FirstOrDefaultAsync(s => s.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"DDS semanal {request.Id} não encontrado.");

        if (semanal.Status == StatusDdsSemanal.Concluida)
            throw new InvalidOperationException("Esta semana já foi encerrada.");

        var dias = semanal.RegistrosDiarios.Where(d => d.Ativo).ToList();
        if (dias.Count < 5)
            throw new InvalidOperationException($"Faltam {5 - dias.Count} dia(s) da semana sem registro de DDS.");
        if (dias.Any(d => d.Status != StatusDds.Concluido))
            throw new InvalidOperationException(
                "Todos os 5 dias precisam estar encerrados (checklist, 3 fotos de evidência e presenças) antes de encerrar a semana.");

        if (semanal.Tipo == TipoDdsSemanal.Terceirizados)
        {
            if (string.IsNullOrWhiteSpace(request.ResponsavelEmpresaTerceirizadaNome) || string.IsNullOrWhiteSpace(request.ResponsavelEmpresaTerceirizadaFuncao))
                throw new InvalidOperationException("Informe nome e função do responsável da empresa terceirizada para encerrar.");
        }

        var usuario = string.IsNullOrEmpty(request.AzureAdObjectId)
            ? null
            : await _db.Usuarios.FirstOrDefaultAsync(u => u.AzureAdObjectId == request.AzureAdObjectId, ct);
        if (usuario is null)
            throw new InvalidOperationException(
                "Seu usuário não está vinculado a um cadastro reconhecido. Peça a um administrador para revisar seu acesso antes de encerrar a semana.");

        semanal.Status = StatusDdsSemanal.Concluida;
        semanal.ResponsavelObraSstUsuarioId = usuario.Id;
        semanal.ResponsavelEmpresaTerceirizadaNome = semanal.Tipo == TipoDdsSemanal.Terceirizados ? request.ResponsavelEmpresaTerceirizadaNome : null;
        semanal.ResponsavelEmpresaTerceirizadaFuncao = semanal.Tipo == TipoDdsSemanal.Terceirizados ? request.ResponsavelEmpresaTerceirizadaFuncao : null;
        semanal.EncerradaEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }
}
