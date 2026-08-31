using AAHBRANT.SST.Application.Assinatura.Queries;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura;

// Movido de IAutenticacaoAssinaturaService.cs (31/08) quando PIN/crachá-QR e WebAuthn/FIDO2 foram
// removidos do sistema (decisão do usuário: único método de assinatura é o Futronic FS80H) — o
// record continua compartilhado pelas estratégias que restaram (Futronic, sessão logada).
public record ResultadoAutenticacaoAssinatura(Guid TrabalhadorId, MetodoAutenticacaoAssinatura Metodo);

// Extraído de RegistrarAssinaturaCommandHandler na etapa 13: gravar o DocumentoSignatario + trilha de
// auditoria é idêntico não importa qual estratégia autenticou o trabalhador, só muda como o
// ResultadoAutenticacaoAssinatura foi obtido — cada estratégia tem sua própria cerimônia, mas o que
// acontece depois de autenticado é sempre o mesmo.
public interface IRegistradorAssinaturaService
{
    Task<DocumentoSignatarioDto> RegistrarAsync(Guid documentoAssinaturaId, ResultadoAutenticacaoAssinatura resultado, string? ipAddress, CancellationToken ct);
}

public class RegistradorAssinaturaService : IRegistradorAssinaturaService
{
    private readonly IAppDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public RegistradorAssinaturaService(IAppDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task<DocumentoSignatarioDto> RegistrarAsync(Guid documentoAssinaturaId, ResultadoAutenticacaoAssinatura resultado, string? ipAddress, CancellationToken ct)
    {
        var documento = await _db.DocumentosAssinatura.FirstOrDefaultAsync(d => d.Id == documentoAssinaturaId, ct);
        if (documento is null)
            throw new KeyNotFoundException("Documento de assinatura não encontrado.");
        if (documento.Status != StatusDocumentoAssinatura.EmAndamento)
            throw new InvalidOperationException("Este documento não está mais aceitando assinaturas.");

        var jaAssinou = await _db.DocumentoSignatarios.AnyAsync(
            s => s.DocumentoAssinaturaId == documento.Id && s.TrabalhadorId == resultado.TrabalhadorId, ct);
        if (jaAssinou)
            throw new InvalidOperationException("Este trabalhador já assinou este documento.");

        var trabalhador = await _db.Trabalhadores.FirstAsync(t => t.Id == resultado.TrabalhadorId, ct);

        var signatario = new DocumentoSignatario
        {
            DocumentoAssinaturaId = documento.Id,
            TrabalhadorId = resultado.TrabalhadorId,
            MetodoAutenticacao = resultado.Metodo,
            AssinadoEm = DateTime.UtcNow,
            IpAddress = ipAddress,
        };
        _db.DocumentoSignatarios.Add(signatario);

        await _auditoria.RegistrarAsync(
            "Assinatura.Registrada",
            documento.EntidadeTipo,
            documento.EntidadeId,
            usuarioId: null,
            trabalhadorId: trabalhador.Id,
            dadosDepois: new { DocumentoAssinaturaId = documento.Id, TrabalhadorId = trabalhador.Id, TrabalhadorNome = trabalhador.Nome, signatario.MetodoAutenticacao, signatario.AssinadoEm },
            ct);

        await _db.SaveChangesAsync(ct);

        return new DocumentoSignatarioDto(trabalhador.Id, trabalhador.Nome, signatario.MetodoAutenticacao, signatario.AssinadoEm, signatario.IpAddress);
    }
}
