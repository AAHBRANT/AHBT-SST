// Ponte entre a API navigator.credentials do navegador e o Fido2NetLib do backend (etapa 13 do Motor
// de Assinatura Eletrônica — docs/Motor-Assinatura-Eletronica.md §3). O backend troca JSON puro
// (CredentialCreateOptions/AssertionOptions.ToJson() e AuthenticatorAttestationRawResponse/
// AuthenticatorAssertionRawResponse) para não acoplar a Application ao pacote Fido2NetLib; aqui só
// convertemos entre esse JSON (bytes em base64url) e os ArrayBuffers que a WebAuthn API exige.
// Formato confirmado por inspeção direta do Fido2NetLib v4.0.1 via reflection (não documentação/suposição).

function base64UrlParaArrayBuffer(base64Url: string): ArrayBuffer {
  const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
  const pad = base64.length % 4 === 0 ? '' : '='.repeat(4 - (base64.length % 4));
  const binario = atob(base64 + pad);
  const bytes = new Uint8Array(binario.length);
  for (let i = 0; i < binario.length; i++) bytes[i] = binario.charCodeAt(i);
  return bytes.buffer;
}

function arrayBufferParaBase64Url(buffer: ArrayBuffer): string {
  const bytes = new Uint8Array(buffer);
  let binario = '';
  for (let i = 0; i < bytes.length; i++) binario += String.fromCharCode(bytes[i]);
  return btoa(binario).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

export function estaWebAuthnDisponivel(): boolean {
  return typeof window !== 'undefined' && typeof window.PublicKeyCredential !== 'undefined';
}

interface DescritorCredencialJson {
  type: string;
  id: string;
  transports?: string[];
}

interface OpcoesCadastroJson {
  rp: { id?: string; name: string };
  user: { id: string; name: string; displayName: string };
  challenge: string;
  pubKeyCredParams: { type: string; alg: number }[];
  timeout?: number;
  attestation?: string;
  authenticatorSelection?: {
    authenticatorAttachment?: string;
    residentKey?: string;
    requireResidentKey?: boolean;
    userVerification?: string;
  };
  excludeCredentials?: DescritorCredencialJson[];
}

interface OpcoesAutenticacaoJson {
  challenge: string;
  timeout?: number;
  rpId?: string;
  allowCredentials?: DescritorCredencialJson[];
  userVerification?: string;
}

// Passo do navegador na cerimônia de CADASTRO: recebe o opcoesJson devolvido por
// IniciarCadastroWebAuthnCommand, chama navigator.credentials.create() e devolve o respostaJson pronto
// para ConfirmarCadastroWebAuthnCommand.
export async function criarCredencialWebAuthn(opcoesJson: string): Promise<string> {
  const opcoes = JSON.parse(opcoesJson) as OpcoesCadastroJson;

  const publicKey: PublicKeyCredentialCreationOptions = {
    rp: opcoes.rp,
    user: {
      id: base64UrlParaArrayBuffer(opcoes.user.id),
      name: opcoes.user.name,
      displayName: opcoes.user.displayName,
    },
    challenge: base64UrlParaArrayBuffer(opcoes.challenge),
    pubKeyCredParams: opcoes.pubKeyCredParams as PublicKeyCredentialParameters[],
    timeout: opcoes.timeout,
    attestation: opcoes.attestation as AttestationConveyancePreference | undefined,
    authenticatorSelection: opcoes.authenticatorSelection as AuthenticatorSelectionCriteria | undefined,
    excludeCredentials: opcoes.excludeCredentials?.map((c) => ({
      type: 'public-key' as const,
      id: base64UrlParaArrayBuffer(c.id),
      transports: c.transports as AuthenticatorTransport[] | undefined,
    })),
  };

  const credencial = (await navigator.credentials.create({ publicKey })) as PublicKeyCredential | null;
  if (!credencial) throw new Error('O navegador não retornou uma credencial.');
  const resposta = credencial.response as AuthenticatorAttestationResponse;

  return JSON.stringify({
    id: arrayBufferParaBase64Url(credencial.rawId),
    rawId: arrayBufferParaBase64Url(credencial.rawId),
    type: credencial.type,
    response: {
      attestationObject: arrayBufferParaBase64Url(resposta.attestationObject),
      clientDataJSON: arrayBufferParaBase64Url(resposta.clientDataJSON),
      transports: resposta.getTransports?.() ?? [],
    },
    clientExtensionResults: credencial.getClientExtensionResults?.() ?? {},
  });
}

// Passo do navegador na cerimônia de AUTENTICAÇÃO (assinatura): recebe o opcoesJson devolvido por
// IniciarAssinaturaWebAuthnCommand, chama navigator.credentials.get() e devolve o respostaJson pronto
// para ConfirmarAutenticacaoWebAuthnCommand.
export async function obterAssercaoWebAuthn(opcoesJson: string): Promise<string> {
  const opcoes = JSON.parse(opcoesJson) as OpcoesAutenticacaoJson;

  const publicKey: PublicKeyCredentialRequestOptions = {
    challenge: base64UrlParaArrayBuffer(opcoes.challenge),
    timeout: opcoes.timeout,
    rpId: opcoes.rpId,
    userVerification: opcoes.userVerification as UserVerificationRequirement | undefined,
    allowCredentials: opcoes.allowCredentials?.map((c) => ({
      type: 'public-key' as const,
      id: base64UrlParaArrayBuffer(c.id),
      transports: c.transports as AuthenticatorTransport[] | undefined,
    })),
  };

  const credencial = (await navigator.credentials.get({ publicKey })) as PublicKeyCredential | null;
  if (!credencial) throw new Error('O navegador não retornou uma credencial.');
  const resposta = credencial.response as AuthenticatorAssertionResponse;

  return JSON.stringify({
    id: arrayBufferParaBase64Url(credencial.rawId),
    rawId: arrayBufferParaBase64Url(credencial.rawId),
    type: credencial.type,
    response: {
      authenticatorData: arrayBufferParaBase64Url(resposta.authenticatorData),
      signature: arrayBufferParaBase64Url(resposta.signature),
      clientDataJSON: arrayBufferParaBase64Url(resposta.clientDataJSON),
      userHandle: resposta.userHandle ? arrayBufferParaBase64Url(resposta.userHandle) : null,
    },
    clientExtensionResults: credencial.getClientExtensionResults?.() ?? {},
  });
}
