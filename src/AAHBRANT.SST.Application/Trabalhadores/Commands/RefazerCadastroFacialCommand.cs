using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Commands;

// Quem cadastra o facial (permissão trabalhador:assinatura, em geral o técnico) refaz o cadastro, mas
// só de trabalhador da obra a que tem acesso. Remove o cadastro no Azure Face e ARQUIVA as fotos de
// cadastro (Ativo = false, nunca apaga: o log de assinaturas de EPI usa a foto vigente na data de cada
// assinatura antiga como prova, via IgnoreQueryFilters). O cadastro único (30/09) volta a valer depois
// da nova captura. As assinaturas já feitas continuam válidas.
public record RefazerCadastroFacialCommand(Guid TrabalhadorId, Guid UsuarioId, string Motivo) : IRequest;

public class RefazerCadastroFacialCommandValidator : AbstractValidator<RefazerCadastroFacialCommand>
{
    public RefazerCadastroFacialCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.UsuarioId).NotEmpty();
        RuleFor(x => x.Motivo).NotEmpty().MinimumLength(5).MaximumLength(500)
            .WithMessage("Informe o motivo (de 5 a 500 caracteres).");
    }
}

public class RefazerCadastroFacialCommandHandler : IRequestHandler<RefazerCadastroFacialCommand>
{
    private readonly IAppDbContext _db;
    private readonly IAutenticacaoFacialService _facial;
    private readonly IAuditoriaService _auditoria;
    private readonly ICurrentUserService _usuarioAtual;

    public RefazerCadastroFacialCommandHandler(IAppDbContext db, IAutenticacaoFacialService facial, IAuditoriaService auditoria, ICurrentUserService usuarioAtual)
    {
        _db = db;
        _facial = facial;
        _auditoria = auditoria;
        _usuarioAtual = usuarioAtual;
    }

    public async Task Handle(RefazerCadastroFacialCommand request, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == request.TrabalhadorId, ct)
            ?? throw new KeyNotFoundException("Trabalhador não encontrado.");

        // Além do filtro global por obra do DbContext, confere de forma explícita: funcionário de outra
        // obra responde como "não encontrado", sem revelar que ele existe.
        if (!_usuarioAtual.TemAcessoGlobal && !_usuarioAtual.ObrasPermitidas.Contains(trabalhador.ObraId))
            throw new KeyNotFoundException("Trabalhador não encontrado.");

        var fotos = await _db.FotosCadastroFacial.Where(f => f.TrabalhadorId == request.TrabalhadorId).ToListAsync(ct);
        if (fotos.Count == 0)
            throw new InvalidOperationException("Este trabalhador não tem cadastro facial para refazer.");

        // Azure primeiro: se falhar, nada local foi tocado e dá para tentar de novo. Se o banco falhar
        // depois, repetir é seguro porque a remoção no Azure é idempotente.
        await _facial.RemoverCadastroAsync(request.TrabalhadorId, ct);

        // A trilha guarda quem, quando, por quê e o hash das fotos arquivadas (prova do que existia).
        // Os bytes ficam só nas fotos arquivadas, nunca na trilha.
        await _auditoria.RegistrarAsync(
            "Trabalhador.CadastroFacialRefeito",
            "Trabalhador",
            trabalhador.Id,
            usuarioId: request.UsuarioId,
            trabalhadorId: trabalhador.Id,
            dadosDepois: new
            {
                Motivo = request.Motivo.Trim(),
                FotosArquivadas = fotos.Count,
                HashesSha256 = fotos.Select(f => f.HashSha256).ToArray(),
                CadastradaEm = fotos.Min(f => f.CapturadaEm),
            },
            ct);

        _db.FotosCadastroFacial.RemoveRange(fotos);
        trabalhador.AzureFacePersonId = null;
        await _db.SaveChangesAsync(ct);
    }
}
