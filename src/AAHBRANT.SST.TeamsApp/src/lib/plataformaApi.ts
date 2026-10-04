import { API_BASE_URL } from './apiBase';
import { montarHeadersAuth } from './authHeaders';

export type ModuloPlataforma = 'sst' | 'qualidade';
export interface ObraModulo { id: string; codigo: string; nome: string; cidade: string | null; uf: string | null; status: number }
export interface ContextoModulo { obra: ObraModulo; podeConfigurarTeams: boolean }

async function consultar<T>(caminho: string, signal?: AbortSignal): Promise<T> {
  const headers = await montarHeadersAuth();
  const resposta = await fetch(`${API_BASE_URL}/api/plataforma/${caminho}`, { headers, signal, cache: 'no-store' });
  if (!resposta.ok) {
    if (resposta.status === 401) throw new Error('Entre com sua conta corporativa para continuar.');
    if (resposta.status === 403) throw new Error('Você não tem acesso a este módulo ou a esta obra. Solicite a revisão do seu perfil ao administrador.');
    if (resposta.status === 404) throw new Error('Esta obra não está mais disponível.');
    throw new Error('Não foi possível consultar as obras. Tente novamente.');
  }
  return resposta.json() as Promise<T>;
}

export const plataformaApi = {
  obras: (modulo: ModuloPlataforma, configurarTeams = false, signal?: AbortSignal) =>
    consultar<ObraModulo[]>(`modulos/${modulo}/obras?configurarTeams=${configurarTeams}`, signal),
  contexto: (modulo: ModuloPlataforma, obraId: string, signal?: AbortSignal) =>
    consultar<ContextoModulo>(`modulos/${modulo}/obras/${encodeURIComponent(obraId)}`, signal),
  aba: (modulo: ModuloPlataforma, obraId: string, signal?: AbortSignal) =>
    consultar<{ nome: string; caminho: string }>(`abas/${modulo}/${encodeURIComponent(obraId)}`, signal),
};
