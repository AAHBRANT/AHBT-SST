using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Trabalhadores.Commands;

// O trabalhador retira o consentimento de uso da biometria (LGPD art. 8º §5º). A partir daí ele não assina
// mais por digital nem por rosto: o consentimento é apagado (todas as checagens já exigem ele), o cadastro
// facial sai do Azure e as fotos/digitais de cadastro são arquivadas. As assinaturas já feitas continuam
// válidas. Para voltar a usar biometria, é preciso novo consentimento e novo cadastro.
public record RevogarConsentimentoBiometriaCommand(Guid TrabalhadorId, string Motivo) : IRequest;

public class RevogarConsentimentoBiometriaCommandValidator : AbstractValidator<RevogarConsentimentoBiometriaCommand>
{
    public RevogarConsentimentoBiometriaCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.Motivo).NotEmpty().MinimumLength(5).MaximumLength(500)
            .WithMessage("Informe o motivo (de 5 a 500 caracteres).");
    }
}

public class RevogarConsentimentoBiometriaCommandHandler : IRequestHandler<RevogarConsentimentoBiometriaCommand>
{
    private readonly IAppDbContext _db;
    private readonly IAutenticacaoFacialService _facial;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<RevogarConsentimentoBiometriaCommandHandler> _logger;

    public RevogarConsentimentoBiometriaCommandHandler(IAppDbContext db, IAutenticacaoFacialService facial, IAuditoriaService auditoria,
        ILogger<RevogarConsentimentoBiometriaCommandHandler> logger)
    {
        _db = db;
        _facial = facial;
        _auditoria = auditoria;
        _logger = logger;
    }

    public async Task Handle(RevogarConsentimentoBiometriaCommand request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == request.TrabalhadorId, ct)
            ?? throw new KeyNotFoundException("Trabalhador não encontrado.");
        if (trabalhador.ConsentimentoBiometriaEm is null)
            throw new InvalidOperationException("Este trabalhador não tem consentimento de biometria ativo.");

        await _auditoria.RegistrarAsync(
            "Trabalhador.ConsentimentoBiometriaRevogado", "Trabalhador", trabalhador.Id,
            usuarioId: null, trabalhadorId: trabalhador.Id,
            dadosDepois: new
            {
                Motivo = request.Motivo.Trim(),
                ConsentidoEm = trabalhador.ConsentimentoBiometriaEm,
                Versao = trabalhador.ConsentimentoBiometriaVersao,
            },
            ct);

        trabalhador.ConsentimentoBiometriaEm = null;
        trabalhador.ConsentimentoBiometriaRevogadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await RemocaoBiometriaDesligamento.TentarAsync(_db, _facial, _logger, trabalhador.Id, ct);
    }
}
