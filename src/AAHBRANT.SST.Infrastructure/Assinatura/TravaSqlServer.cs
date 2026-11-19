using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Assinatura;

// Trava exclusiva entre réplicas da API, guardada no SQL Server (sp_getapplock). Usada onde duas réplicas
// fazendo a mesma coisa ao mesmo tempo estragam o resultado — caso real: a reconstrução do grupo facial
// rodando em 3 réplicas juntas deixou o grupo sem treino e com pessoas duplicadas (19/11).
//
// Não espera: se outra réplica já tem a trava, devolve null na hora e quem chamou pula o trabalho. A trava
// fica presa a uma conexão própria e é liberada no Dispose (ou sozinha, se o processo morrer e a conexão cair).
// Fora do SQL Server (banco em memória dos testes) não há outra réplica: devolve uma trava vazia.
internal static class TravaSqlServer
{
    public static async Task<IAsyncDisposable?> TentarAdquirirAsync(object db, string recurso, CancellationToken ct)
    {
        if (db is not DbContext contexto || !contexto.Database.IsSqlServer())
            return new SemTrava();

        var conexao = new SqlConnection(contexto.Database.GetConnectionString());
        try
        {
            await conexao.OpenAsync(ct);
            await using var comando = conexao.CreateCommand();
            comando.CommandText =
                "DECLARE @r int; EXEC @r = sp_getapplock @Resource = @recurso, @LockMode = 'Exclusive', " +
                "@LockOwner = 'Session', @LockTimeout = 0; SELECT @r;";
            comando.Parameters.AddWithValue("@recurso", recurso);
            var resultado = Convert.ToInt32(await comando.ExecuteScalarAsync(ct));
            if (resultado < 0)
            {
                await conexao.DisposeAsync();
                return null;
            }
            return new Trava(conexao, recurso);
        }
        catch
        {
            await conexao.DisposeAsync();
            throw;
        }
    }

    private sealed class Trava(SqlConnection conexao, string recurso) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                // Libera explicitamente: a conexão volta para o pool e a sessão não é zerada na hora.
                await using var comando = conexao.CreateCommand();
                comando.CommandText = "EXEC sp_releaseapplock @Resource = @recurso, @LockOwner = 'Session';";
                comando.Parameters.AddWithValue("@recurso", recurso);
                await comando.ExecuteNonQueryAsync();
            }
            catch (SqlException)
            {
                // Conexão já caiu: o SQL Server liberou a trava junto.
            }
            finally
            {
                await conexao.DisposeAsync();
            }
        }
    }

    private sealed class SemTrava : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
