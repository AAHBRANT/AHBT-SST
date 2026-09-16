using AAHBRANT.SST.Application.Common.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Integracao.Grh;

// Leitura direta das tabelas `alojamentos`/`alojamento_moradores` do banco do G-RH (Integração
// G-RH — leitura de banco em vez de endpoint HTTP, decisão de 2026-09-16, ver GrhDbOptions).
// Somente leitura (SELECT); nenhuma escrita é feita nesse banco.
//
// Schema confirmado por inspeção manual em 2026-09-16 (banco `grh-hml`):
// - alojamentos: id (nvarchar 36), nome, endereco, capacidade (não usado — sempre nulo hoje, sem
//   uso no SST), responsavel/custo_mensal/centro_custo (não usados — ver decisão de não trazer
//   dado financeiro), ativo (bit), criado_em (nvarchar, data como texto).
// - alojamento_moradores: id, alojamento_id, colaborador_id, inicio/fim (nvarchar, data como
//   texto), atual (bit) — só entram como morador ativo quando atual=1 E fim IS NULL (concordância
//   dupla, mais conservador que confiar só numa das duas colunas).
// - colaboradores: usado só pelo cpf, pra casar com Trabalhador no SST.
public class AlojamentoGrhDbClient : IAlojamentoGrhClient
{
    private readonly GrhDbOptions _opcoes;

    public AlojamentoGrhDbClient(IOptions<GrhDbOptions> opcoes)
    {
        _opcoes = opcoes.Value;
    }

    public async Task<IReadOnlyList<AlojamentoGrhDto>> ListarTodosAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_opcoes.ConnectionString))
            throw new InvalidOperationException(
                "GrhDb:ConnectionString não configurado — leitura direta do banco do G-RH ainda não provisionada.");

        await using var conexao = new SqlConnection(_opcoes.ConnectionString);
        await conexao.OpenAsync(ct);

        var alojamentos = new Dictionary<string, (string Nome, string? Endereco, string? CentroCusto, bool Ativo, List<AlojamentoMoradorGrhDto> Moradores)>();

        await using (var comando = new SqlCommand("SELECT id, nome, endereco, centro_custo, ativo FROM alojamentos", conexao))
        await using (var leitor = await comando.ExecuteReaderAsync(ct))
        {
            while (await leitor.ReadAsync(ct))
            {
                var id = leitor.GetString(0);
                alojamentos[id] = (
                    Nome: leitor.GetString(1),
                    Endereco: leitor.IsDBNull(2) ? null : leitor.GetString(2),
                    CentroCusto: leitor.IsDBNull(3) ? null : leitor.GetString(3),
                    Ativo: leitor.GetBoolean(4),
                    Moradores: new List<AlojamentoMoradorGrhDto>());
            }
        }

        const string sqlMoradores = """
            SELECT am.alojamento_id, c.cpf, c.matricula, am.inicio
            FROM alojamento_moradores am
            JOIN colaboradores c ON c.id = am.colaborador_id
            WHERE am.atual = 1 AND am.fim IS NULL
            """;
        await using (var comando = new SqlCommand(sqlMoradores, conexao))
        await using (var leitor = await comando.ExecuteReaderAsync(ct))
        {
            while (await leitor.ReadAsync(ct))
            {
                var alojamentoId = leitor.GetString(0);
                if (!alojamentos.TryGetValue(alojamentoId, out var alojamento))
                    continue; // vínculo órfão (alojamento removido do lado do G-RH) — ignora

                var cpf = leitor.GetString(1);
                var matricula = leitor.IsDBNull(2) ? null : leitor.GetString(2);
                var desde = GrhSqlDateParser.LerColuna(leitor, 3) ?? DateTime.UtcNow;

                alojamento.Moradores.Add(new AlojamentoMoradorGrhDto(cpf, matricula, desde));
            }
        }

        return alojamentos.Select(par => new AlojamentoGrhDto(
            GrhAlojamentoId: par.Key,
            Nome: par.Value.Nome,
            // G-RH não guarda vínculo de obra em `alojamentos` — `centro_custo` é a única pista
            // disponível (nome de consórcio/projeto), usado como candidato a nome de Obra. Se não
            // bater com nenhuma Obra cadastrada no SST, SincronizarAlojamentoGrhCommand rejeita o
            // registro com um erro claro (ver ImportarAlojamentosGrhResultado.Erros) em vez de
            // adivinhar — não há hoje um mapeamento confirmado entre os dois lados.
            ObraNome: par.Value.CentroCusto,
            Endereco: par.Value.Endereco,
            Ativo: par.Value.Ativo,
            Moradores: par.Value.Moradores)).ToList();
    }
}
