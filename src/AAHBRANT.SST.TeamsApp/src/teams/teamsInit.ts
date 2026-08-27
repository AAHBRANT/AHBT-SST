import { app } from '@microsoft/teams-js';

let promessaInicializacao: Promise<boolean> | null = null;

// Fonte única de verdade para app.initialize(): sem isso, qualquer chamada de API disparada
// no mount de uma página (ex.: os 8 fetches em paralelo do Dashboard) pode disparar
// getAuthToken() antes do SDK terminar de inicializar, que falha rápido ("library not
// initialized") e derruba a chamada para 401 sem token — mesmo o usuário estando
// corretamente autenticado/autorizado. Memoizando a promise, tanto useTeamsContext quanto
// a aquisição de token em lib/api.ts esperam o mesmo resultado, não importa qual dispare
// primeiro nem quantas chamadas concorrentes aconteçam no mount.
export function aguardarInicializacaoTeams(): Promise<boolean> {
  if (!promessaInicializacao) {
    promessaInicializacao = app
      .initialize()
      .then(() => true)
      .catch(() => false);
  }
  return promessaInicializacao;
}
