using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PermissoesTrabalho.Commands;

// §7 "Liberação da atividade" — as três assinaturas nomeadas do documento: Emitente/Responsável
// pela Área (AutorizadoPorUsuarioId, obrigatório), Responsável pela Execução (confirma ciência no
// mesmo ato — DataAssinaturaExecucao) e Responsável SST "quando requerido" (ResponsavelSstUsuarioId
// opcional). Bloqueia a liberação se: (a) algum dos 6 pré-requisitos do §2 não estiver Atendido, ou
// (b) alguma das 15 verificações do §4 estiver marcada NaoConforme — texto literal do documento:
// "nenhuma atividade poderá iniciar com item crítico NC". Verificação ainda não respondida (null)
// não bloqueia — decisão própria, já que o documento não exige explicitamente que todos os 15
// itens estejam respondidos antes de liberar, só que nenhum esteja NC.
public record AutorizarPermissaoTrabalhoCommand(Guid Id, Guid AutorizadoPorUsuarioId, Guid? ResponsavelSstUsuarioId) : IRequest;

public class AutorizarPermissaoTrabalhoCommandValidator : AbstractValidator<AutorizarPermissaoTrabalhoCommand>
{
    public AutorizarPermissaoTrabalhoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.AutorizadoPorUsuarioId).NotEmpty();
    }
}

public class AutorizarPermissaoTrabalhoCommandHandler : IRequestHandler<AutorizarPermissaoTrabalhoCommand>
{
    private readonly IAppDbContext _db;

    public AutorizarPermissaoTrabalhoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AutorizarPermissaoTrabalhoCommand request, CancellationToken ct)
    {
        var pt = await _db.PermissoesTrabalho
            .Include(p => p.PreRequisitos)
            .Include(p => p.Verificacoes)
            .FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Permissão de Trabalho {request.Id} não encontrada.");

        if (pt.Status is not (StatusPt.EmElaboracao or StatusPt.Suspensa))
            throw new InvalidOperationException("Só é possível liberar uma PT em elaboração ou suspensa.");

        var usuarioExiste = await _db.Usuarios.AnyAsync(u => u.Id == request.AutorizadoPorUsuarioId, ct);
        if (!usuarioExiste)
            throw new KeyNotFoundException($"Usuário {request.AutorizadoPorUsuarioId} não encontrado.");

        if (request.ResponsavelSstUsuarioId.HasValue &&
            !await _db.Usuarios.AnyAsync(u => u.Id == request.ResponsavelSstUsuarioId, ct))
            throw new KeyNotFoundException($"Usuário {request.ResponsavelSstUsuarioId} não encontrado.");

        var preRequisitosPendentes = pt.PreRequisitos.Where(r => r.Ativo && !r.Atendido).ToList();
        if (preRequisitosPendentes.Count > 0)
            throw new InvalidOperationException(
                $"Não é possível liberar: {preRequisitosPendentes.Count} pré-requisito(s) pendente(s) ({string.Join(", ", preRequisitosPendentes.Select(p => p.Item))}).");

        var verificacoesNaoConforme = pt.Verificacoes
            .Where(v => v.Ativo && v.Resposta == RespostaVerificacaoPt.NaoConforme).ToList();
        if (verificacoesNaoConforme.Count > 0)
            throw new InvalidOperationException(
                $"Não é possível liberar: {verificacoesNaoConforme.Count} verificação(ões) marcada(s) como Não Conforme ({string.Join(", ", verificacoesNaoConforme.Select(v => v.Item))}). Corrija a condição e verifique novamente.");

        var agora = DateTime.UtcNow;
        pt.Status = StatusPt.Autorizada;
        pt.AutorizadoPorUsuarioId = request.AutorizadoPorUsuarioId;
        pt.DataAutorizacao = agora;
        pt.DataAssinaturaExecucao = agora;
        if (request.ResponsavelSstUsuarioId.HasValue)
        {
            pt.ResponsavelSstUsuarioId = request.ResponsavelSstUsuarioId;
            pt.DataAssinaturaSst = agora;
        }

        await _db.SaveChangesAsync(ct);
    }
}
