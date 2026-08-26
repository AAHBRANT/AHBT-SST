using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Assinatura;

// Estratégia biométrica principal do Motor de Assinatura Eletrônica (docs/Motor-Assinatura-
// Eletronica.md §3, etapa 13) — leitor FIDO2 físico da obra e celular próprio usam a mesma cerimônia
// WebAuthn, só o TipoAutenticadorWebAuthn muda. Diferente de CrachaPinAutenticacaoStrategy (um passo
// só), aqui a cerimônia é sempre iniciar → confirmar (desafio/resposta), por isso vive atrás de
// IAutenticacaoWebAuthnService em vez de IAutenticacaoAssinaturaService.
public class Fido2AutenticacaoStrategy : IAutenticacaoWebAuthnService
{
    private readonly IAppDbContext _db;
    private readonly IFido2 _fido2;

    public Fido2AutenticacaoStrategy(IAppDbContext db, IFido2 fido2)
    {
        _db = db;
        _fido2 = fido2;
    }

    public async Task<string> IniciarCadastroAsync(Guid trabalhadorId, TipoAutenticadorWebAuthn tipo, CancellationToken ct)
    {
        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == trabalhadorId, ct);
        if (trabalhador is null)
            throw new KeyNotFoundException("Trabalhador não encontrado.");

        // Mesmo gate jurídico da estratégia crachá+PIN (§4 do doc) — além do Termo de Aceite geral,
        // cadastrar biometria exige o consentimento LGPD específico, já que aqui o autenticador
        // efetivamente processa um dado biométrico do trabalhador (mesmo que o template nunca chegue
        // ao servidor).
        if (trabalhador.TermoAceiteAssinaturaEletronicaEm is null)
            throw new InvalidOperationException("Trabalhador ainda não confirmou o Termo de Aceite de Assinatura Eletrônica.");
        if (trabalhador.ConsentimentoBiometriaEm is null)
            throw new InvalidOperationException("Trabalhador ainda não registrou o consentimento LGPD de uso de biometria.");

        var credenciaisExistentes = await _db.CredenciaisWebAuthn
            .Where(c => c.TrabalhadorId == trabalhadorId)
            .Select(c => c.CredentialId)
            .ToListAsync(ct);

        var usuario = new Fido2User
        {
            Id = trabalhadorId.ToByteArray(),
            Name = trabalhador.Nome,
            DisplayName = trabalhador.Nome,
        };

        var opcoes = _fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = usuario,
            ExcludeCredentials = credenciaisExistentes.Select(id => new PublicKeyCredentialDescriptor(id)).ToList(),
            AuthenticatorSelection = new AuthenticatorSelection
            {
                UserVerification = UserVerificationRequirement.Required,
                // Leitor da obra é compartilhado entre trabalhadores: a credencial precisa ser
                // "discoverable" para o servidor conseguir resolver quem encostou o dedo só pela
                // resposta da assertion (ver UserHandle em CredencialWebAuthn).
                ResidentKey = tipo == TipoAutenticadorWebAuthn.LeitorObra ? ResidentKeyRequirement.Required : ResidentKeyRequirement.Preferred,
            },
            AttestationPreference = AttestationConveyancePreference.None,
        });

        return opcoes.ToJson();
    }

    public async Task ConfirmarCadastroAsync(Guid trabalhadorId, TipoAutenticadorWebAuthn tipo, string opcoesJson, string respostaJson, CancellationToken ct)
    {
        var opcoesOriginais = CredentialCreateOptions.FromJson(opcoesJson);
        var resposta = System.Text.Json.JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(respostaJson)
            ?? throw new InvalidOperationException("Resposta de cadastro WebAuthn inválida.");

        var resultado = await _fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
        {
            AttestationResponse = resposta,
            OriginalOptions = opcoesOriginais,
            IsCredentialIdUniqueToUserCallback = async (parametros, cancelamento) =>
            {
                var existe = await _db.CredenciaisWebAuthn.AnyAsync(c => c.CredentialId == parametros.CredentialId, cancelamento);
                return !existe;
            },
        }, cancellationToken: ct);

        _db.CredenciaisWebAuthn.Add(new CredencialWebAuthn
        {
            TrabalhadorId = trabalhadorId,
            Tipo = tipo,
            CredentialId = resultado.Id,
            PublicKey = resultado.PublicKey,
            UserHandle = resultado.User.Id,
            SignCount = resultado.SignCount,
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<string> IniciarAutenticacaoAsync(Guid? trabalhadorId, CancellationToken ct)
    {
        List<PublicKeyCredentialDescriptor> credenciaisPermitidas;
        if (trabalhadorId is null)
        {
            // Leitor da obra: não sabe de antemão quem vai encostar o dedo — lista vazia = qualquer
            // credencial "discoverable" cadastrada neste relying party pode responder (ver ConfirmarAutenticacaoAsync).
            credenciaisPermitidas = new List<PublicKeyCredentialDescriptor>();
        }
        else
        {
            var idsTrabalhador = await _db.CredenciaisWebAuthn
                .Where(c => c.TrabalhadorId == trabalhadorId.Value)
                .Select(c => c.CredentialId)
                .ToListAsync(ct);
            if (idsTrabalhador.Count == 0)
                throw new InvalidOperationException("Trabalhador não possui credencial WebAuthn cadastrada.");
            credenciaisPermitidas = idsTrabalhador.Select(id => new PublicKeyCredentialDescriptor(id)).ToList();
        }

        var opcoes = _fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = credenciaisPermitidas,
            UserVerification = UserVerificationRequirement.Required,
        });

        return opcoes.ToJson();
    }

    public async Task<ResultadoAutenticacaoAssinatura> ConfirmarAutenticacaoAsync(string opcoesJson, string respostaJson, CancellationToken ct)
    {
        var opcoesOriginais = AssertionOptions.FromJson(opcoesJson);
        var resposta = System.Text.Json.JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(respostaJson)
            ?? throw new InvalidOperationException("Resposta de autenticação WebAuthn inválida.");

        var credencial = await _db.CredenciaisWebAuthn.FirstOrDefaultAsync(c => c.CredentialId == resposta.RawId, ct);
        if (credencial is null)
            throw new KeyNotFoundException("Credencial WebAuthn não encontrada.");

        var resultado = await _fido2.MakeAssertionAsync(new MakeAssertionParams
        {
            AssertionResponse = resposta,
            OriginalOptions = opcoesOriginais,
            StoredPublicKey = credencial.PublicKey,
            StoredSignatureCounter = credencial.SignCount,
            IsUserHandleOwnerOfCredentialIdCallback = async (parametros, cancelamento) =>
            {
                var pertence = await _db.CredenciaisWebAuthn.AnyAsync(
                    c => c.CredentialId == parametros.CredentialId && c.UserHandle == parametros.UserHandle, cancelamento);
                return pertence;
            },
        }, cancellationToken: ct);

        var trabalhador = await _db.Trabalhadores.FirstOrDefaultAsync(t => t.Id == credencial.TrabalhadorId, ct);
        if (trabalhador is null)
            throw new KeyNotFoundException("Trabalhador vinculado à credencial não encontrado.");

        var obra = await _db.Obras.FirstOrDefaultAsync(o => o.Id == trabalhador.ObraId, ct);
        if (obra is null || !obra.MetodosAutenticacaoHabilitados.HasFlag(MetodoAutenticacaoObra.Biometria))
            throw new InvalidOperationException("Este método de assinatura não está habilitado para a obra deste trabalhador.");

        credencial.SignCount = resultado.SignCount;
        credencial.UltimoUsoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var metodo = credencial.Tipo == TipoAutenticadorWebAuthn.LeitorObra
            ? MetodoAutenticacaoAssinatura.Biometria
            : MetodoAutenticacaoAssinatura.WebAuthnCelular;

        return new ResultadoAutenticacaoAssinatura(trabalhador.Id, metodo);
    }
}
