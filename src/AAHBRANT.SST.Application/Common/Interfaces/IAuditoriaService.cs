namespace AAHBRANT.SST.Application.Common.Interfaces;

// Abstração sobre a gravação em TrilhaAuditoria (Domain/Entidades/Evidencia.cs) — Application não
// pode calcular a cadeia de hash diretamente porque isso é lógica de infraestrutura (System.Security.
// Cryptography + acesso ao último registro), mesma regra de dependência já aplicada a IPinHasher.
// Todo handler que precisar registrar um evento append-only na trilha depende desta interface, nunca
// grava em TrilhaAuditoria diretamente.
public interface IAuditoriaService
{
    // Só adiciona o registro ao contexto (Add) — não chama SaveChangesAsync. Fica a cargo do handler
    // chamador salvar tudo junto (evento de auditoria + a mudança de negócio) em uma única transação,
    // mesmo padrão de "vários Add, um SaveChanges" já usado nos handlers de Assinatura.
    Task RegistrarAsync(
        string acao,
        string entidadeTipo,
        Guid entidadeId,
        Guid? usuarioId,
        Guid? trabalhadorId,
        object? dadosDepois,
        CancellationToken ct);
}
