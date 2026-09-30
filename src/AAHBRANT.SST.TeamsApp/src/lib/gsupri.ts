import { API_BASE_URL } from './apiBase';
import { montarHeadersAuth } from './authHeaders';

export interface VinculoProdutoGsupri {
  codigoExterno: string; unidade: string; categoria: string; catalogoId: string | null;
  tamanho: string; fatorConversao: number;
}
export interface ItemGsupri {
  itemId: string; produtoCodigo: string; descricao: string; unidade: string;
  quantidadeRecebida: number; quantidadeLiberada: number | null; quantidadeAplicada: number; pendencia: string | null;
}
export interface RecebimentoGsupri {
  id: string; recebimentoId: string; versao: number; pedidoId: string; obraCodigo: string;
  obraId: string | null; numeroNota: string; status: string; pendencia: string | null;
  recebidoEm: string; itens: ItemGsupri[];
}
export interface PainelGsupri {
  habilitada: boolean; obras: { codigoExterno: string; obraId: string; nome: string }[];
  produtos: VinculoProdutoGsupri[]; recebimentos: RecebimentoGsupri[]; totalRecebimentos: number;
}

// Integração administrativa exige conexão; não enfileirar/reexecutar em sincronização offline.
async function request<T>(path: string, method = 'GET', body?: unknown): Promise<T> {
  const response = await fetch(`${API_BASE_URL}/api/integracoes/gsupri${path}`, {
    method, headers: { 'Content-Type': 'application/json', ...await montarHeadersAuth() },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!response.ok) {
    const data = await response.json().catch(() => null);
    throw new Error(data?.erro ?? data?.title ?? `Não foi possível acessar a integração (HTTP ${response.status}).`);
  }
  return response.status === 204 ? undefined as T : response.json();
}
export const gsupri = {
  opcoes: () => request<{ obras: { id: string; nome: string }[]; catalogos: Record<string, { id: string; nome: string }[]> }>('/opcoes'),
  painel: (pagina: number) => request<PainelGsupri>(`/painel?pagina=${pagina}`),
  obra: (codigoExterno: string, obraId: string) => request<void>('/vinculos/obras', 'PUT', { codigoExterno, obraId }),
  produto: (dados: VinculoProdutoGsupri) => request<void>('/vinculos/produtos', 'PUT', dados),
  reprocessar: (id: string) => request<RecebimentoGsupri>(`/recebimentos/${id}/reprocessar`, 'POST'),
};
