using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Auditoria;

// Primeiro e único gravador de TrilhaAuditoria hoje (docs/Motor-Assinatura-Eletronica.md §5, etapa 7)
// — a entidade já existia como esqueleto (só ListarTrilhaAuditoriaQuery consultava). Cadeia de hash
// simples estilo blockchain: cada registro guarda o HashRegistroAtual do registro anterior em
// HashRegistroAnterior, então qualquer alteração retroativa de uma linha quebra o hash de todas as
// seguintes — só SHA-256 puro (sem chave), porque o objetivo é evidenciar adulteração, não
// confidencialidade (qualquer um com acesso ao banco já vê os dados em texto claro).
//
// Limitação conhecida e aceita nesta etapa: a leitura do "último registro" + gravação do novo não é
// atômica (sem lock/serialização de transação), então duas gravações concorrentes na trilha podem ler
// o mesmo HashRegistroAnterior e "bifurcar" a cadeia. Para o volume de uso atual (assinatura de DDS,
// poucas gravações por minuto) o risco é baixo; se isso virar um requisito forte, a correção é fazer a
// leitura+escrita dentro de uma transação SERIALIZABLE ou usar um contador/sequence dedicado.
public class AuditoriaService : IAuditoriaService
{
    private const string HashGenese = "GENESIS";

    private readonly IAppDbContext _db;

    public AuditoriaService(IAppDbContext db) => _db = db;

    public async Task RegistrarAsync(
        string acao,
        string entidadeTipo,
        Guid entidadeId,
        Guid? usuarioId,
        Guid? trabalhadorId,
        object? dadosDepois,
        CancellationToken ct)
    {
        var ultimoRegistro = await _db.TrilhaAuditoria
            .OrderByDescending(t => t.Timestamp)
            .Select(t => t.HashRegistroAtual)
            .FirstOrDefaultAsync(ct);

        var hashAnterior = ultimoRegistro ?? HashGenese;
        var timestamp = DateTime.UtcNow;
        var dadosDepoisJson = dadosDepois is null ? null : JsonSerializer.Serialize(dadosDepois);

        var registro = new TrilhaAuditoria
        {
            Timestamp = timestamp,
            UsuarioId = usuarioId,
            TrabalhadorId = trabalhadorId,
            Acao = acao,
            EntidadeTipo = entidadeTipo,
            EntidadeId = entidadeId,
            DadosDepoisJson = dadosDepoisJson,
            HashRegistroAnterior = hashAnterior,
            HashRegistroAtual = CalcularHash(hashAnterior, acao, entidadeTipo, entidadeId, usuarioId, trabalhadorId, timestamp, dadosDepoisJson),
        };

        _db.TrilhaAuditoria.Add(registro);
    }

    private static string CalcularHash(
        string hashAnterior,
        string acao,
        string entidadeTipo,
        Guid entidadeId,
        Guid? usuarioId,
        Guid? trabalhadorId,
        DateTime timestamp,
        string? dadosDepoisJson)
    {
        var conteudo = string.Join('|',
            hashAnterior, acao, entidadeTipo, entidadeId, usuarioId, trabalhadorId, timestamp.ToString("O"), dadosDepoisJson);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(conteudo));
        return Convert.ToHexString(hash);
    }
}
