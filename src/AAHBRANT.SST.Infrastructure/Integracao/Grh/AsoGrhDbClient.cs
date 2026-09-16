using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.Grh;

// Leitura direta da tabela `documentos` do banco do G-RH, filtrada pelo tipo "ASO" (Integração
// G-RH — leitura de banco em vez de endpoint HTTP, decisão de 2026-09-16, ver GrhDbOptions). O
// G-RH não tem uma tabela dedicada de ASO — exames ficam em `documentos`, uma tabela genérica de
// documentos do colaborador, categorizada por `tipos_documento.codigo`. Somente leitura.
//
// Schema confirmado por inspeção manual em 2026-09-16: `aptidao`/`restricao_clinica`/
// `medico_responsavel` vêm nulos na maioria dos registros hoje (upload em andamento pelo usuário) —
// isso é esperado, ver SincronizarAsoGrhCommand pra regra de não sobrescrever dado clínico já
// lançado no SST. `documentos` não distingue subtipo de ASO (admissional/periódico/demissional).
public class AsoGrhDbClient : IAsoGrhClient
{
    private readonly GrhDbOptions _opcoes;

    public AsoGrhDbClient(IOptions<GrhDbOptions> opcoes)
    {
        _opcoes = opcoes.Value;
    }

    public async Task<IReadOnlyList<AsoGrhDto>> ListarTodosAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opcoes.ConnectionString))
            throw new InvalidOperationException(
                "GrhDb:ConnectionString não configurado — leitura direta do banco do G-RH ainda não provisionada.");

        await using var conexao = new SqlConnection(_opcoes.ConnectionString);
        await conexao.OpenAsync(ct);

        const string sql = """
            SELECT d.id, c.cpf, d.numero, d.emissao, d.validade, d.aptidao, d.restricao_clinica, d.medico_responsavel
            FROM documentos d
            JOIN tipos_documento t ON t.id = d.tipo_id
            JOIN colaboradores c ON c.id = d.colaborador_id
            WHERE t.codigo = 'ASO'
            """;

        var resultado = new List<AsoGrhDto>();
        await using var comando = new SqlCommand(sql, conexao);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct))
        {
            var dataExame = GrhSqlDateParser.LerColuna(leitor, 3);
            if (dataExame is null)
                continue; // data de emissão é a própria identidade temporal do exame — sem ela, não há o que sincronizar

            resultado.Add(new AsoGrhDto(
                GrhAsoId: leitor.GetString(0),
                Cpf: leitor.GetString(1),
                Numero: leitor.IsDBNull(2) ? null : leitor.GetString(2),
                DataExame: dataExame.Value,
                DataValidade: GrhSqlDateParser.LerColuna(leitor, 4),
                Aptidao: leitor.IsDBNull(5) ? null : leitor.GetString(5),
                RestricaoClinica: leitor.IsDBNull(6) ? null : leitor.GetString(6),
                MedicoNome: leitor.IsDBNull(7) ? null : leitor.GetString(7)));
        }

        return resultado;
    }
}
