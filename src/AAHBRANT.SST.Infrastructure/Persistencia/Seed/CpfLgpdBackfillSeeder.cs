using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

// Seeder de migração de dado (não de catálogo, ver RbacSeeder para o padrão geral): roda a cada start
// da API e é idempotente (WHERE CpfHash IS NULL — uma vez migrada, a linha nunca mais aparece aqui).
//
// Usa ADO.NET cru (Microsoft.Data.SqlClient) em vez de SstDbContext deliberadamente: o
// ValueConverter mapeado em Cpf tentaria descriptografar todo valor lido da coluna, e os CPFs
// legados ainda estão em texto plano — passar por ele quebraria a leitura. Este seeder é a única
// peça do sistema com permissão de tocar a coluna Cpf sem passar pelo conversor.
public static class CpfLgpdBackfillSeeder
{
    public static async Task ExecutarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("SstDatabase")
            ?? throw new InvalidOperationException("Connection string 'SstDatabase' não configurada.");

        await using var conexao = new SqlConnection(connectionString);
        await conexao.OpenAsync(ct);

        var pendentes = new List<(Guid Id, string CpfPlano)>();
        await using (var comandoSelecao = new SqlCommand("SELECT Id, Cpf FROM Trabalhadores WHERE CpfHash IS NULL", conexao))
        await using (var leitor = await comandoSelecao.ExecuteReaderAsync(ct))
        {
            while (await leitor.ReadAsync(ct))
                pendentes.Add((leitor.GetGuid(0), leitor.GetString(1)));
        }

        foreach (var (id, cpfPlano) in pendentes)
        {
            var cpfCriptografado = CpfCriptografiaConversor.Criptografar(cpfPlano);
            var cpfHash = CpfCriptografiaConversor.CalcularHash(cpfPlano);

            await using var comandoAtualizacao = new SqlCommand(
                "UPDATE Trabalhadores SET Cpf = @cpf, CpfHash = @hash WHERE Id = @id", conexao);
            comandoAtualizacao.Parameters.AddWithValue("@cpf", cpfCriptografado);
            comandoAtualizacao.Parameters.AddWithValue("@hash", cpfHash);
            comandoAtualizacao.Parameters.AddWithValue("@id", id);
            await comandoAtualizacao.ExecuteNonQueryAsync(ct);
        }
    }
}
