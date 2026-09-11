import {
  BrowserAuthError,
  InteractionRequiredAuthError,
  PublicClientApplication,
  type AccountInfo,
} from '@azure/msal-browser';

const CLIENT_ID_PADRAO_HML = 'a7a22c22-5b01-4fd6-8c40-d4ddbd7d4904';
const ESCOPO_PADRAO_HML =
  'api://sst-web-hml.kindground-7a44c4f0.brazilsouth.azurecontainerapps.io/a7a22c22-5b01-4fd6-8c40-d4ddbd7d4904/access_as_user';

const clientId = import.meta.env.VITE_ENTRA_CLIENT_ID || CLIENT_ID_PADRAO_HML;
const tenantId = import.meta.env.VITE_ENTRA_TENANT_ID;
const apiScope = import.meta.env.VITE_ENTRA_API_SCOPE || ESCOPO_PADRAO_HML;

let appMsal: PublicClientApplication | null = null;
let inicializacaoMsal: Promise<PublicClientApplication | null> | null = null;
let redirectProcessado: Promise<void> | null = null;

export class LoginNavegadorNaoConfiguradoError extends Error {
  constructor() {
    super(
      'Login Microsoft no navegador ainda não está configurado neste ambiente. Configure VITE_ENTRA_TENANT_ID no build do frontend.',
    );
    this.name = 'LoginNavegadorNaoConfiguradoError';
  }
}

export class LoginNavegadorEmAndamentoError extends Error {
  constructor() {
    super('Redirecionando para o login Microsoft. Conclua a entrada para continuar.');
    this.name = 'LoginNavegadorEmAndamentoError';
  }
}

function authBrowserConfigurado(): boolean {
  return Boolean(clientId && tenantId && apiScope);
}

async function obterMsal(): Promise<PublicClientApplication | null> {
  if (!authBrowserConfigurado()) {
    return null;
  }

  if (!appMsal) {
    appMsal = new PublicClientApplication({
      auth: {
        clientId,
        authority: `https://login.microsoftonline.com/${tenantId}`,
        redirectUri: window.location.origin + window.location.pathname,
        postLogoutRedirectUri: window.location.origin + window.location.pathname,
      },
      cache: {
        cacheLocation: 'localStorage',
      },
    });
  }

  inicializacaoMsal ??= appMsal.initialize().then(() => appMsal);
  return inicializacaoMsal;
}

async function processarRedirect(msal: PublicClientApplication): Promise<void> {
  redirectProcessado ??= msal.handleRedirectPromise().then((resultado) => {
    if (resultado?.account) {
      msal.setActiveAccount(resultado.account);
    }
  });
  return redirectProcessado;
}

function selecionarConta(msal: PublicClientApplication): AccountInfo | null {
  const ativa = msal.getActiveAccount();
  if (ativa) {
    return ativa;
  }

  const [primeiraConta] = msal.getAllAccounts();
  if (primeiraConta) {
    msal.setActiveAccount(primeiraConta);
    return primeiraConta;
  }

  return null;
}

export async function obterTokenAutenticacaoNavegador(): Promise<string | null> {
  const msal = await obterMsal();
  if (!msal) {
    throw new LoginNavegadorNaoConfiguradoError();
  }

  await processarRedirect(msal);
  const account = selecionarConta(msal);
  const requisicao = { scopes: [apiScope], account: account ?? undefined };

  if (!account) {
    await msal.loginRedirect({ scopes: [apiScope], prompt: 'select_account' });
    throw new LoginNavegadorEmAndamentoError();
  }

  try {
    const resultado = await msal.acquireTokenSilent(requisicao);
    return resultado.accessToken;
  } catch (erro) {
    if (erro instanceof InteractionRequiredAuthError || erro instanceof BrowserAuthError) {
      await msal.acquireTokenRedirect(requisicao);
      throw new LoginNavegadorEmAndamentoError();
    }
    throw erro;
  }
}
