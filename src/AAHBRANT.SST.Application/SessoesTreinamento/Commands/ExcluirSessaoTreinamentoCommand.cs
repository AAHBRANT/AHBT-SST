using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SessoesTreinamento.Commands;

// Exclusão DEFINITIVA de turma (pedido explícito do usuário, 10/11/2026; mockup turma-excluir-2026-11-10). Só Administrador
// (a policy fica no controller). Diferente de toda outra exclusão do sistema, que é lógica (SstDbContext.AplicarAuditoria):
// aqui a turma, as inscrições e as fotos de evidência saem do banco (IAppDbContext.MarcarExclusaoFisica). As fotos ficam
// no próprio banco (varbinary), então não há arquivo em blob para limpar.
//
// Antes de apagar, grava na trilha de auditoria um retrato do que existia (sem imagem, biometria ou CPF) e salva tudo em
// um único SaveChanges (uma transação): ou apaga e registra, ou não faz nada.
public record ExcluirSessaoTreinamentoCommand(Guid Id) : IRequest;

public class ExcluirSessaoTreinamentoCommandValidator : AbstractValidator<ExcluirSessaoTreinamentoCommand>
{
    public ExcluirSessaoTreinamentoCommandValidator() => RuleFor(x => x.Id).NotEmpty();
}

public class ExcluirSessaoTreinamentoCommandHandler : IRequestHandler<ExcluirSessaoTreinamentoCommand>
{
    private readonly IAppDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly ICurrentUserService _usuario;

    public ExcluirSessaoTreinamentoCommandHandler(IAppDbContext db, IAuditoriaService auditoria, ICurrentUserService usuario)
    {
        _db = db;
        _auditoria = auditoria;
        _usuario = usuario;
    }

    public async Task Handle(ExcluirSessaoTreinamentoCommand request, CancellationToken ct)
    {
        var resultado = await ExclusaoSessaoTreinamento.ExecutarAsync(_db, _auditoria, _usuario.UsuarioId, request.Id, ct);
        if (resultado.Status == StatusExclusaoTurma.NaoEncontrada)
            throw new KeyNotFoundException(resultado.Motivo ?? $"Turma de treinamento {request.Id} não encontrada.");
        if (resultado.Status is StatusExclusaoTurma.Bloqueada or StatusExclusaoTurma.Falhou)
            throw new InvalidOperationException(resultado.Motivo);
    }
}

public enum StatusExclusaoTurma { Excluida, Bloqueada, NaoEncontrada, Falhou }

public record ResultadoExclusaoTurma(Guid Id, StatusExclusaoTurma Status, string? Motivo, string? Numero = null);

// Regra compartilhada pela exclusão individual, pelo lote e pela prévia.
internal static class ExclusaoSessaoTreinamento
{
    public const string AcaoAuditoria = "SessaoTreinamento.ExcluidaDefinitivamente";
    public const string TipoDocumentoAta = "SessaoTreinamento";

    // Motivo do bloqueio, ou nulo se a turma pode ser excluída. Todas as checagens ignoram filtros globais: certificado
    // excluído logicamente continua preso por FK (NO_ACTION) e continua sendo documento do trabalhador.
    public static async Task<(int Certificados, string? Motivo)> AvaliarBloqueioAsync(IAppDbContext db, Guid sessaoId, CancellationToken ct)
    {
        var certificados = await db.Treinamentos.IgnoreQueryFilters().CountAsync(t => t.SessaoTreinamentoId == sessaoId, ct);
        if (certificados > 0)
        {
            return (certificados, certificados == 1
                ? "Esta turma tem 1 certificado emitido. Certificado é documento do trabalhador e precisa continuar valendo, então a turma não pode ser excluída."
                : $"Esta turma tem {certificados} certificados emitidos. Certificado é documento do trabalhador e precisa continuar valendo, então a turma não pode ser excluída.");
        }

        var comAssinatura = await (
            from d in db.DocumentosAssinatura.IgnoreQueryFilters()
            where d.EntidadeTipo == TipoDocumentoAta && d.EntidadeId == sessaoId
            join s in db.DocumentoSignatarios.IgnoreQueryFilters() on d.Id equals s.DocumentoAssinaturaId
            select s.Id).AnyAsync(ct);
        if (comAssinatura)
            return (0, "Esta turma tem um documento com assinatura já registrada, e documento assinado precisa continuar valendo. Por isso ela não pode ser excluída.");

        // PONTO DE EXTENSÃO: "turma pertence a uma integração em andamento" (mockup, situação 3). Depende de uma entidade
        // que liga a turma à integração, que ainda não existe no domínio; quando existir, a checagem entra aqui e devolve
        // o motivo (ex.: "Esta turma pertence a uma integração em andamento..."). Não foi inventada para não adivinhar o modelo.

        return (0, null);
    }

    // A exclusão é irreversível, então o escopo do Administrador é checado aqui, e não só pela policy global (que aprova quem é
    // Administrador em QUALQUER vínculo): precisa ser Administrador na obra da turma ou Administrador global (vínculo com
    // ObraId nulo). Sem usuário resolvido (Entra desligado em dev) mantém o comportamento de dev; token de serviço nem passa
    // pela policy SomenteAdministrador.
    public static async Task<bool> EhAdministradorNaObraAsync(IAppDbContext db, Guid? usuarioId, Guid obraId, CancellationToken ct)
    {
        if (usuarioId is null) return true;
        return await db.UsuariosPerfilObra.IgnoreQueryFilters()
            .AnyAsync(v => v.UsuarioId == usuarioId && v.Ativo
                && v.PerfilAcesso != null && v.PerfilAcesso.Ativo && v.PerfilAcesso.Tipo == TipoPerfilAcesso.Administrador
                && (v.ObraId == null || v.ObraId == obraId), ct);
    }

    public static async Task<ResultadoExclusaoTurma> ExecutarAsync(
        IAppDbContext db, IAuditoriaService auditoria, Guid? usuarioId, Guid sessaoId, CancellationToken ct)
    {
        // Filtro global cobre o escopo por obra (Administrador restrito a obras só enxerga as dele) e turma já excluída.
        var sessao = await db.SessoesTreinamento
            .Include(s => s.Obra)
            .Include(s => s.CursoTreinamento)
            .FirstOrDefaultAsync(s => s.Id == sessaoId, ct);
        if (sessao is null)
            return new ResultadoExclusaoTurma(sessaoId, StatusExclusaoTurma.NaoEncontrada, "Turma não encontrada.");

        // Fora da regra: não revela que a turma existe.
        if (!await EhAdministradorNaObraAsync(db, usuarioId, sessao.ObraId, ct))
            return new ResultadoExclusaoTurma(sessaoId, StatusExclusaoTurma.NaoEncontrada, "Turma não encontrada.");

        var numero = sessao.NumeroCertificado;
        var (_, motivo) = await AvaliarBloqueioAsync(db, sessaoId, ct);
        if (motivo is not null)
            return new ResultadoExclusaoTurma(sessaoId, StatusExclusaoTurma.Bloqueada, motivo, numero);

        // IgnoreQueryFilters: inscrição/foto já excluídas logicamente também precisam sair, senão ficariam apontando para a turma.
        var participantes = await db.ParticipantesSessaoTreinamento.IgnoreQueryFilters()
            .Where(p => p.SessaoTreinamentoId == sessaoId).ToListAsync(ct);
        var fotos = await db.FotosEvidenciaSessaoTreinamento.IgnoreQueryFilters()
            .Where(f => f.SessaoTreinamentoId == sessaoId).ToListAsync(ct);
        // Documentos da Ata sem nenhuma assinatura (a checagem acima garante que não há assinada): só rastreio da turma.
        var documentosSemAssinatura = await db.DocumentosAssinatura.IgnoreQueryFilters()
            .Where(d => d.EntidadeTipo == TipoDocumentoAta && d.EntidadeId == sessaoId).ToListAsync(ct);

        var ativos = participantes.Where(p => p.Ativo).ToList();
        var retrato = new
        {
            Numero = numero,
            Curso = sessao.CursoTreinamento?.Nome,
            ObraId = sessao.ObraId,
            Obra = sessao.Obra?.Nome,
            Data = sessao.DataRealizacao,
            Status = sessao.Status.ToString(),
            Inscricoes = ativos.Count,
            Presencas = ativos.Count(p => p.PresencaConfirmadaEm is not null),
            FotosEvidencia = fotos.Count(f => f.Ativo),
            DocumentosRastreioRemovidos = documentosSemAssinatura.Count,
            ParticipantesTrabalhadorIds = participantes.Select(p => p.TrabalhadorId).ToList(),
        };

        try
        {
            await auditoria.RegistrarAsync(AcaoAuditoria, nameof(SessaoTreinamento), sessao.Id, usuarioId, null, retrato, ct);

            foreach (var p in participantes) db.MarcarExclusaoFisica(p);
            foreach (var f in fotos) db.MarcarExclusaoFisica(f);
            foreach (var d in documentosSemAssinatura) db.MarcarExclusaoFisica(d);
            db.MarcarExclusaoFisica(sessao);

            await db.SaveChangesAsync(ct);
            return new ResultadoExclusaoTurma(sessaoId, StatusExclusaoTurma.Excluida, null, numero);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Outro Administrador excluiu a turma entre a leitura e a gravação.
            db.DescartarAlteracoesPendentes();
            return new ResultadoExclusaoTurma(sessaoId, StatusExclusaoTurma.NaoEncontrada, "Esta turma já foi excluída.", numero);
        }
        catch (DbUpdateException)
        {
            // Falha de banco (ex.: alguma referência que não prevemos): nada foi apagado nem registrado; no lote segue para a próxima.
            db.DescartarAlteracoesPendentes();
            return new ResultadoExclusaoTurma(sessaoId, StatusExclusaoTurma.Falhou,
                "Não foi possível excluir esta turma agora porque ela ainda está ligada a outros registros. Nada foi apagado.", numero);
        }
    }
}
