using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Common;

// Trabalhador.CpfHash tem índice único (ver CpfCriptografiaConversor) para impedir dois cadastros
// com o mesmo CPF, já que o próprio Cpf criptografado não serve mais de chave (nonce aleatório por
// gravação). Sem este tratamento, a violação de índice chega ao TratamentoDeExcecaoMiddleware como
// DbUpdateException genérica e vira "Ocorreu um erro inesperado" — mensagem que não diz ao usuário
// que o problema é um CPF já cadastrado. Detecta pela mensagem da exceção (não referencia
// Microsoft.Data.SqlClient diretamente: este projeto não depende de um provedor de banco específico).
public static class TratamentoCpfDuplicado
{
    public static async Task SalvarAsync(IAppDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_Trabalhadores_CpfHash") == true)
        {
            throw new InvalidOperationException("CPF já cadastrado para outro trabalhador.");
        }
    }
}
