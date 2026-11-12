using System.Text;
using System.Text.Json;
using AAHBRANT.SST.AgenteCameras.Opcoes;
using AAHBRANT.SST.AgenteCameras.Seguranca;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.AgenteCameras.Servidor;

// Última lista de câmeras vinda do servidor, para o serviço religar as câmeras depois de um reinício sem internet.
// O arquivo inteiro (com as senhas) é protegido com DPAPI (IProtetorDados) e fica na pasta de dados, que já tem o
// acesso restrito (PermissaoPasta). Falha ao ler ou gravar nunca derruba o serviço.
public sealed class CacheCameras
{
    public const string NomeArquivo = "cameras-servidor.dat";

    private sealed record Conteudo(string AgenteId, List<CameraConfig> Cameras);

    private readonly string _caminho;
    private readonly IProtetorDados _protetor;
    private readonly string _agenteId;
    private readonly ILogger _log;
    private string? _ultimoGravado;

    public CacheCameras(string pastaDados, IProtetorDados protetor, string agenteId, ILogger log)
    {
        _caminho = Path.Combine(pastaDados, NomeArquivo);
        _protetor = protetor;
        _agenteId = agenteId;
        _log = log;
    }

    public string Caminho => _caminho;

    public IReadOnlyList<CameraConfig> Carregar()
    {
        try
        {
            if (!File.Exists(_caminho)) return Array.Empty<CameraConfig>();
            var json = Encoding.UTF8.GetString(_protetor.Desproteger(File.ReadAllBytes(_caminho)));
            var conteudo = JsonSerializer.Deserialize<Conteudo>(json);
            if (conteudo is null || !string.Equals(conteudo.AgenteId, _agenteId, StringComparison.OrdinalIgnoreCase))
            {
                _log.LogWarning("O cache local de câmeras pertence a outro agente; ignorado.");
                return Array.Empty<CameraConfig>();
            }
            _ultimoGravado = json;
            return conteudo.Cameras;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException
            or System.Security.Cryptography.CryptographicException or ArgumentException)
        {
            // Mensagem sem detalhe de conteúdo: o arquivo tem senhas.
            _log.LogWarning("Não foi possível ler o cache local de câmeras ({Tipo}); ele será refeito na próxima consulta ao servidor.", e.GetType().Name);
            return Array.Empty<CameraConfig>();
        }
    }

    /// <summary>Apaga o arquivo de cache (agente revogado: as senhas das câmeras do servidor não podem continuar no disco).</summary>
    public void Apagar()
    {
        try
        {
            if (File.Exists(_caminho)) File.Delete(_caminho);
            _ultimoGravado = null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _log.LogWarning("Não foi possível apagar o cache local de câmeras ({Tipo}).", e.GetType().Name);
        }
    }

    /// <summary>Grava a lista se ela mudou desde a última gravação. Escrita atômica (arquivo temporário + troca).</summary>
    public void Salvar(IReadOnlyList<CameraConfig> cameras)
    {
        try
        {
            var json = JsonSerializer.Serialize(new Conteudo(_agenteId, cameras.OrderBy(c => c.CameraId, StringComparer.Ordinal).ToList()));
            if (json == _ultimoGravado) return;
            Directory.CreateDirectory(Path.GetDirectoryName(_caminho)!);
            var temporario = _caminho + ".tmp";
            File.WriteAllBytes(temporario, _protetor.Proteger(Encoding.UTF8.GetBytes(json)));
            File.Move(temporario, _caminho, overwrite: true);
            _ultimoGravado = json;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        {
            _log.LogWarning("Não foi possível gravar o cache local de câmeras ({Tipo}); o serviço segue com a configuração em memória.", e.GetType().Name);
        }
    }
}
