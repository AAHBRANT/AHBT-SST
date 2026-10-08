using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Queries;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Commands;

// Assinatura dos dois campos do Registro Semanal de DDS (02/10, pedido do usuário): o técnico de
// segurança assina, com botões separados, como "Responsável/Treinador pelo DDS" e como
// "Responsável da Obra/SST". É a assinatura com um clique da sessão logada (mesmo método dos
// outros responsáveis do sistema), gravada no Motor de Assinatura com o Papel — é isso que deixa a
// mesma pessoa assinar o mesmo documento duas vezes, uma em cada função, e que o PDF saiba qual
// campo preencher. Sem assinatura gravada, o campo não aparece assinado no PDF.
public record AssinarDdsSemanalCommand(Guid DdsSemanalId, PapelAssinatura Papel, string? AzureAdObjectId, string? IpAddress = null)
    : IRequest<DocumentoSignatarioDto>;

public class AssinarDdsSemanalCommandValidator : AbstractValidator<AssinarDdsSemanalCommand>
{
    public AssinarDdsSemanalCommandValidator()
    {
        RuleFor(x => x.DdsSemanalId).NotEmpty();
        RuleFor(x => x.Papel).IsInEnum();
    }
}

public class AssinarDdsSemanalCommandHandler : IRequestHandler<AssinarDdsSemanalCommand, DocumentoSignatarioDto>
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    private readonly IRegistradorAssinaturaService _registrador;

    public AssinarDdsSemanalCommandHandler(IMediator mediator, IAppDbContext db, IRegistradorAssinaturaService registrador)
    {
        _mediator = mediator;
        _db = db;
        _registrador = registrador;
    }

    public async Task<DocumentoSignatarioDto> Handle(AssinarDdsSemanalCommand request, CancellationToken ct)
    {
        var existe = await _db.DdsSemanais.AnyAsync(s => s.Id == request.DdsSemanalId, ct);
        if (!existe)
            throw new KeyNotFoundException($"DDS semanal {request.DdsSemanalId} não encontrado.");

        var usuario = string.IsNullOrEmpty(request.AzureAdObjectId)
            ? null
            : await _db.Usuarios.FirstOrDefaultAsync(u => u.AzureAdObjectId == request.AzureAdObjectId, ct);
        if (usuario?.TrabalhadorId is null)
            throw new InvalidOperationException(
                "Seu usuário não está vinculado a um cadastro de trabalhador. Peça a um administrador para vincular seu usuário antes de assinar.");

        var documentoId = await _mediator.Send(
            new Assinatura.Commands.CriarDocumentoAssinaturaCommand(nameof(Domain.Entidades.DdsSemanal), request.DdsSemanalId), ct);

        var resultado = new ResultadoAutenticacaoAssinatura(usuario.TrabalhadorId.Value, MetodoAutenticacaoAssinatura.SessaoLogada);
        try
        {
            return await _registrador.RegistrarAsync(documentoId, resultado, request.IpAddress, ct, papel: request.Papel);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("já assinou", StringComparison.Ordinal))
        {
            var campo = request.Papel == PapelAssinatura.ResponsavelDds ? "Responsável/Treinador pelo DDS" : "Responsável da Obra/SST";
            throw new InvalidOperationException($"Você já assinou o campo \"{campo}\" desta semana.");
        }
    }
}
