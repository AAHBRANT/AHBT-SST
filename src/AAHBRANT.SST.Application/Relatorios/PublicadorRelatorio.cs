using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Relatorios;

public record NovoRelatorio(
    TipoRelatorio Tipo,
    Guid? ObraId,
    string ChaveUnica,
    string Titulo,
    string Resumo,
    byte[] Imagem,
    byte[]? Pdf,
    string? PdfNome,
    Guid? ReferenciaId,
    DateTime? PeriodoInicio,
    DateTime? PeriodoFim);

// Guarda o relatório e o entrega: Telegram (imagem, depois o PDF) e sininho do Teams para os destinatários
// cadastrados. Cada relatório sai uma única vez (ChaveUnica). Nenhuma falha de entrega derruba o processo: o
// resultado de cada canal fica registrado em RelatorioEnvio.
public interface IPublicadorRelatorio
{
    // Devolve o Id do relatório, ou null se esse relatório já tinha sido publicado antes.
    Task<Guid?> PublicarAsync(NovoRelatorio relatorio, CancellationToken ct = default);
}

public class PublicadorRelatorioService : IPublicadorRelatorio
{
    private const int TamanhoMaximoErro = 500;

    private readonly IAppDbContext _db;
    private readonly ITelegramResumoService _telegram;
    private readonly INotificacaoTeamsService _sininho;
    private readonly ILogger<PublicadorRelatorioService> _logger;

    public PublicadorRelatorioService(
        IAppDbContext db,
        ITelegramResumoService telegram,
        INotificacaoTeamsService sininho,
        ILogger<PublicadorRelatorioService> logger)
    {
        _db = db;
        _telegram = telegram;
        _sininho = sininho;
        _logger = logger;
    }

    public async Task<Guid?> PublicarAsync(NovoRelatorio r, CancellationToken ct = default)
    {
        if (await _db.RelatoriosGerados.IgnoreQueryFilters().AnyAsync(x => x.ChaveUnica == r.ChaveUnica, ct))
            return null;

        var relatorio = new RelatorioGerado
        {
            Tipo = r.Tipo,
            ObraId = r.ObraId,
            ChaveUnica = r.ChaveUnica,
            Titulo = r.Titulo,
            Resumo = r.Resumo,
            Imagem = r.Imagem,
            Pdf = r.Pdf,
            PdfNome = r.PdfNome,
            ReferenciaId = r.ReferenciaId,
            PeriodoInicio = r.PeriodoInicio,
            PeriodoFim = r.PeriodoFim,
            GeradoEm = DateTime.UtcNow,
        };
        _db.RelatoriosGerados.Add(relatorio);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Outra instância publicou o mesmo relatório entre a checagem e a gravação.
            return null;
        }

        var envios = new List<RelatorioEnvio>();
        await EntregarNoTelegramAsync(relatorio, envios, ct);
        await EntregarNoSininhoAsync(relatorio, envios, ct);

        _db.RelatorioEnvios.AddRange(envios);
        await _db.SaveChangesAsync(ct);
        return relatorio.Id;
    }

    private async Task EntregarNoTelegramAsync(RelatorioGerado r, List<RelatorioEnvio> envios, CancellationToken ct)
    {
        try
        {
            var imagemOk = await _telegram.EnviarImagemAsync(r.Imagem, r.Resumo, ct);
            if (!imagemOk)
            {
                // Sem a imagem, o resumo chega em texto: o aviso nunca deixa de chegar.
                await _telegram.EnviarAsync(r.Resumo, ct);
            }
            if (r.Pdf is { Length: > 0 } pdf)
                await _telegram.EnviarDocumentoAsync(pdf, r.PdfNome ?? "relatorio.pdf", "Detalhe em PDF", ct);
            envios.Add(Envio(r, CanalEnvioRelatorio.Telegram, null, true, imagemOk ? null : "Imagem recusada; enviado em texto."));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao entregar o relatório {Chave} no Telegram.", r.ChaveUnica);
            envios.Add(Envio(r, CanalEnvioRelatorio.Telegram, null, false, ex.Message));
        }
    }

    private async Task EntregarNoSininhoAsync(RelatorioGerado r, List<RelatorioEnvio> envios, CancellationToken ct)
    {
        List<Guid> usuarios;
        try
        {
            usuarios = await ObterDestinatariosAsync(r, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao listar os destinatários do relatório {Chave}.", r.ChaveUnica);
            envios.Add(Envio(r, CanalEnvioRelatorio.Sininho, null, false, ex.Message));
            return;
        }

        foreach (var usuarioId in usuarios)
        {
            try
            {
                await _sininho.EnviarAsync(usuarioId, r.Titulo, r.Resumo, ct);
                envios.Add(Envio(r, CanalEnvioRelatorio.Sininho, usuarioId, true, null));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao avisar o usuário {UsuarioId} sobre o relatório {Chave}.", usuarioId, r.ChaveUnica);
                envios.Add(Envio(r, CanalEnvioRelatorio.Sininho, usuarioId, false, ex.Message));
            }
        }
    }

    private async Task<List<Guid>> ObterDestinatariosAsync(RelatorioGerado r, CancellationToken ct)
    {
        var candidatos = _db.DestinatariosRelatorio.Where(d => r.ObraId == null ? d.ObraId == null : (d.ObraId == null || d.ObraId == r.ObraId));
        candidatos = r.Tipo switch
        {
            TipoRelatorio.ListaPresencaDds => candidatos.Where(d => d.ListaPresenca),
            TipoRelatorio.BoletimSemanal => candidatos.Where(d => d.BoletimSemanal),
            TipoRelatorio.Ocorrencia => candidatos.Where(d => d.Ocorrencia),
            _ => candidatos.Where(_ => false),
        };
        var ids = await candidatos.Select(d => d.UsuarioId).Distinct().ToListAsync(ct);
        if (ids.Count == 0) return ids;
        return await _db.Usuarios.Where(u => ids.Contains(u.Id) && u.Status == StatusUsuario.Ativo).Select(u => u.Id).ToListAsync(ct);
    }

    private static RelatorioEnvio Envio(RelatorioGerado r, CanalEnvioRelatorio canal, Guid? usuarioId, bool sucesso, string? erro) => new()
    {
        RelatorioGeradoId = r.Id,
        Canal = canal,
        UsuarioId = usuarioId,
        Sucesso = sucesso,
        Erro = erro is { Length: > TamanhoMaximoErro } ? erro[..TamanhoMaximoErro] : erro,
    };
}
