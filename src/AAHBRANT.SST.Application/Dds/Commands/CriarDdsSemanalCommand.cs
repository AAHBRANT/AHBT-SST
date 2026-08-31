using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Commands;

// DDS Semanal (31/08, modelo em papel do usuário) — container da semana; os 5 registros diários
// (Dds) são criados depois, um a um, vinculados aqui via DdsSemanalId (ver CriarDdsCommand).
// "Responsável/Treinador pelo DDS" é sempre o usuário logado que abre a semana (decisão do
// usuário) — por isso o command recebe AzureAdObjectId (claim "oid"), não um Guid escolhido em
// tela, mesmo padrão de RegistrarAssinaturaSessaoLogadaCommand.
public record CriarDdsSemanalCommand(
    Guid ObraId,
    TipoDdsSemanal Tipo,
    string? EmpresaTerceirizada,
    string? NumeroDocumento,
    string? LocalFrenteServico,
    DateTime DataInicioSemana,
    string? AzureAdObjectId) : IRequest<Guid>;

public class CriarDdsSemanalCommandValidator : AbstractValidator<CriarDdsSemanalCommand>
{
    public CriarDdsSemanalCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.DataInicioSemana).NotEmpty();
        RuleFor(x => x.EmpresaTerceirizada)
            .NotEmpty().When(x => x.Tipo == TipoDdsSemanal.Terceirizados)
            .WithMessage("Informe a empresa terceirizada.")
            .MaximumLength(200);
        RuleFor(x => x.NumeroDocumento).MaximumLength(50);
        RuleFor(x => x.LocalFrenteServico).MaximumLength(200);
    }
}

public class CriarDdsSemanalCommandHandler : IRequestHandler<CriarDdsSemanalCommand, Guid>
{
    private readonly IAppDbContext _db;

    public CriarDdsSemanalCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(CriarDdsSemanalCommand request, CancellationToken ct)
    {
        var obraExiste = await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct);
        if (!obraExiste)
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var usuario = string.IsNullOrEmpty(request.AzureAdObjectId)
            ? null
            : await _db.Usuarios.FirstOrDefaultAsync(u => u.AzureAdObjectId == request.AzureAdObjectId, ct);
        if (usuario is null)
            throw new InvalidOperationException(
                "Seu usuário não está vinculado a um cadastro reconhecido. Peça a um administrador para revisar seu acesso antes de abrir um DDS semanal.");

        var segunda = SegundaDaSemana(request.DataInicioSemana);

        var semanal = new DdsSemanal
        {
            ObraId = request.ObraId,
            Tipo = request.Tipo,
            EmpresaTerceirizada = request.Tipo == TipoDdsSemanal.Terceirizados ? request.EmpresaTerceirizada : null,
            NumeroDocumento = request.NumeroDocumento,
            LocalFrenteServico = request.LocalFrenteServico,
            ResponsavelUsuarioId = usuario.Id,
            DataInicioSemana = segunda,
            DataFimSemana = segunda.AddDays(4),
        };

        _db.DdsSemanais.Add(semanal);
        await _db.SaveChangesAsync(ct);
        return semanal.Id;
    }

    // A "Semana" do documento em papel é sempre segunda a sexta — qualquer data escolhida na tela
    // (mesmo um fim de semana) é ajustada para a segunda-feira daquela semana ISO.
    private static DateTime SegundaDaSemana(DateTime data)
    {
        var diff = ((int)data.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return data.Date.AddDays(-diff);
    }
}
