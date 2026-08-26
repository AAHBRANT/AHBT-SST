using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Infrastructure.Assinatura;

// Estratégia de reserva do Motor de Assinatura Eletrônica (docs/Motor-Assinatura-Eletronica.md §3/§5,
// etapa 4) — reaproveita TagIdentificacao (mesma tabela/Uid do módulo de Identificação, ver
// ResolverTagPorUidQuery) e adiciona o gate de PIN. Atende tanto crachá NFC/QR quanto QR Code "solto":
// o método que entra no resultado (CrachaPin vs QrCodePin) só depende de TagIdentificacao.Tipo, a
// verificação em si é idêntica — por isso uma única classe atende os dois itens do file tree do doc.
public class CrachaPinAutenticacaoStrategy : IAutenticacaoAssinaturaService
{
    private readonly IAppDbContext _db;

    public CrachaPinAutenticacaoStrategy(IAppDbContext db) => _db = db;

    public async Task<ResultadoAutenticacaoAssinatura> AutenticarPorCrachaOuQrAsync(string uid, string pin, CancellationToken ct)
    {
        var tag = await _db.TagsIdentificacao.FirstOrDefaultAsync(t => t.Uid == uid, ct);
        if (tag is null)
            throw new KeyNotFoundException("Crachá/QR não encontrado.");
        if (tag.Status != StatusTag.Vinculada || tag.EntidadeVinculadaTipo != TipoEntidadeVinculada.Trabalhador || tag.EntidadeVinculadaId is null)
            throw new InvalidOperationException("Este crachá/QR não está vinculado a um trabalhador ativo.");

        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == tag.EntidadeVinculadaId.Value, ct);
        if (trabalhador is null)
            throw new KeyNotFoundException("Trabalhador não encontrado.");

        var metodo = tag.Tipo == TipoTag.QrCode ? MetodoAutenticacaoAssinatura.QrCodePin : MetodoAutenticacaoAssinatura.CrachaPin;
        var metodoObra = metodo == MetodoAutenticacaoAssinatura.QrCodePin ? MetodoAutenticacaoObra.QrCodePin : MetodoAutenticacaoObra.CrachaPin;

        // Cada obra decide quais métodos aceita (§2/§3 do doc) — sem essa checagem, um crachá válido
        // de uma obra que só habilitou biometria ainda conseguiria assinar por PIN.
        var obra = await _db.Obras.FirstOrDefaultAsync(o => o.Id == trabalhador.ObraId, ct);
        if (obra is null || !obra.MetodosAutenticacaoHabilitados.HasFlag(metodoObra))
            throw new InvalidOperationException("Este método de assinatura não está habilitado para a obra deste trabalhador.");

        // Validade jurídica (§4 do doc): o Termo de Aceite cobre tanto biometria quanto o método de
        // reserva, então é exigido aqui também — ConsentimentoBiometriaEm NÃO é checado neste fluxo,
        // pois é específico da estratégia biométrica (o trabalhador pode preferir nunca consentir com
        // biometria e usar só crachá+PIN).
        if (trabalhador.TermoAceiteAssinaturaEletronicaEm is null)
            throw new InvalidOperationException("Trabalhador ainda não confirmou o Termo de Aceite de Assinatura Eletrônica.");

        if (string.IsNullOrEmpty(trabalhador.PinHash))
            throw new InvalidOperationException("Trabalhador ainda não cadastrou um PIN de assinatura.");

        // InvalidOperationException (não UnauthorizedAccessException): TratamentoDeExcecaoMiddleware
        // não tem handler para 401, então cairia no 500 genérico em vez do 400 esperado para esta
        // falha de regra de negócio.
        if (!PinHasher.Verificar(pin, trabalhador.PinHash))
            throw new InvalidOperationException("PIN incorreto.");

        return new ResultadoAutenticacaoAssinatura(trabalhador.Id, metodo);
    }
}
