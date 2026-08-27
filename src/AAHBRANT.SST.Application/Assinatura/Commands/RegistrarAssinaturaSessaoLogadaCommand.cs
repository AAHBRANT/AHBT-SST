using AAHBRANT.SST.Application.Assinatura.Queries;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura.Commands;

// Assinatura em um clique do usuário logado (ex.: entregador de EPI assinando com a própria sessão,
// sem crachá/PIN/biometria) — resolve o TrabalhadorId a partir do AzureAdObjectId (claim "oid") do
// usuário autenticado e delega o registro ao mesmo IRegistradorAssinaturaService das outras
// estratégias. Falha com mensagem amigável (400, via TratamentoDeExcecaoMiddleware) quando o usuário
// não está vinculado a um Trabalhador — a entrega/documento em si já foi salvo antes desta chamada,
// então essa falha bloqueia só a assinatura do entregador, não o registro que a originou.
public record RegistrarAssinaturaSessaoLogadaCommand(Guid DocumentoAssinaturaId, string? AzureAdObjectId) : IRequest<DocumentoSignatarioDto>;

public class RegistrarAssinaturaSessaoLogadaCommandValidator : AbstractValidator<RegistrarAssinaturaSessaoLogadaCommand>
{
    public RegistrarAssinaturaSessaoLogadaCommandValidator()
    {
        RuleFor(x => x.DocumentoAssinaturaId).NotEmpty();
    }
}

public class RegistrarAssinaturaSessaoLogadaCommandHandler : IRequestHandler<RegistrarAssinaturaSessaoLogadaCommand, DocumentoSignatarioDto>
{
    private readonly IAppDbContext _db;
    private readonly IRegistradorAssinaturaService _registrador;

    public RegistrarAssinaturaSessaoLogadaCommandHandler(IAppDbContext db, IRegistradorAssinaturaService registrador)
    {
        _db = db;
        _registrador = registrador;
    }

    public async Task<DocumentoSignatarioDto> Handle(RegistrarAssinaturaSessaoLogadaCommand request, CancellationToken ct)
    {
        var usuario = string.IsNullOrEmpty(request.AzureAdObjectId)
            ? null
            : await _db.Usuarios.FirstOrDefaultAsync(u => u.AzureAdObjectId == request.AzureAdObjectId, ct);

        if (usuario?.TrabalhadorId is null)
            throw new InvalidOperationException(
                "Seu usuário não está vinculado a um cadastro de trabalhador. Peça a um administrador para vincular seu usuário antes de assinar.");

        var resultado = new ResultadoAutenticacaoAssinatura(usuario.TrabalhadorId.Value, MetodoAutenticacaoAssinatura.SessaoLogada);
        return await _registrador.RegistrarAsync(request.DocumentoAssinaturaId, resultado, ct);
    }
}
