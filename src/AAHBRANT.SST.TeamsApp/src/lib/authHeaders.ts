import { authentication } from '@microsoft/teams-js';
import { aguardarInicializacaoTeams } from '../teams/teamsInit';

// Espera a mesma promise de app.initialize() usada por useTeamsContext antes de tentar obter o
// token: chamar getAuthToken() antes do SDK terminar de assentar falha rápido ("library not
// initialized"), derrubando a chamada para 401 mesmo com o usuário autenticado/autorizado no
// Teams (ver AAHBRANT.SST.TeamsApp/src/teams/teamsInit.ts). Importante: NÃO condicionamos a
// tentativa ao resultado (dentroDoTeams) — em alguns hosts reais do Teams (ex.: cliente web)
// app.initialize()/getContext() podem falhar mesmo com getAuthToken() funcionando normalmente
// depois, então só usamos a promise para esperar o SDK assentar, não para decidir se tentamos.
async function obterTokenAutenticacaoTeams(): Promise<string | null> {
  await aguardarInicializacaoTeams();
  try {
    return await Promise.race([
      authentication.getAuthToken(),
      new Promise<null>((resolve) => setTimeout(() => resolve(null), 3000)),
    ]);
  } catch {
    return null;
  }
}

// Extraído para módulo próprio (em vez de viver só em api.ts) porque o motor de sincronização
// offline (lib/offline/syncEngine.ts) também precisa montar o header de autenticação — tanto nas
// chamadas síncronas quanto ao reenviar a fila de mutações pendentes, quando o token capturado no
// momento do enfileiramento já pode ter expirado. api.ts importa este módulo (não o contrário) para
// evitar import circular com syncEngine.ts.
export async function montarHeadersAuth(): Promise<Record<string, string>> {
  const token = await obterTokenAutenticacaoTeams();
  return token ? { Authorization: `Bearer ${token}` } : {};
}
