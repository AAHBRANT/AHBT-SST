import { useState } from 'react';
import { QueryClient, useQuery, type QueryKey, type UseQueryResult } from '@tanstack/react-query';
import { api } from './api';

// Cache de leituras compartilhado entre telas (melhoria de 17/10, piloto no módulo Pessoas). Antes,
// cada aba buscava tudo do zero em um useEffect a cada abertura — trocar de Funcionários para Funções
// e voltar repetia as mesmas 4 chamadas. Agora a tela mostra na hora o último dado já visto e
// atualiza em segundo plano.
//
// staleTime 0 de propósito: toda montagem revalida com o servidor. Telas ainda não migradas (ex.:
// TrabalhadorDetalhePage) alteram esses dados sem avisar o cache — com dado "fresco" por N segundos,
// a lista poderia voltar desatualizada depois de uma edição feita lá.
// retry desligado e sem refetch ao focar a janela: mantém o comportamento de erro de antes (mensagem
// imediata, sem 3 tentativas silenciosas) e não dispara requisições a cada troca de aba no Teams.
// O motor offline (lib/offline) continua por baixo de api.ts — o cache aqui é só em memória.
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: { staleTime: 0, retry: false, refetchOnWindowFocus: false },
  },
});

export const chaves = {
  trabalhadores: ['trabalhadores'],
  obras: ['obras'],
  funcoes: ['funcoes'],
  asos: ['asos'],
} as const satisfies Record<string, QueryKey>;

// Array vazio estável: `data ?? []` criaria um array novo a cada render, e telas que têm a lista
// como dependência de useEffect (ex.: miniaturas em TrabalhadoresTab) entrariam em loop.
const SEM_ITENS: never[] = [];

function useLista<T>(chave: QueryKey, buscar: () => Promise<T[]>) {
  const consulta = useQuery({ queryKey: chave, queryFn: buscar });
  return { ...consulta, lista: consulta.data ?? (SEM_ITENS as T[]) };
}

export const useTrabalhadores = () => useLista(chaves.trabalhadores, () => api.trabalhadores.listar());
export const useObras = () => useLista(chaves.obras, () => api.obras.listar());
export const useFuncoes = () => useLista(chaves.funcoes, () => api.funcoes.listar());
export const useAsos = () => useLista(chaves.asos, () => api.asos.listar());

// Mensagem de erro de carga das consultas, dispensável pelo usuário (botão fechar do FeedbackInline).
// Um erro novo (recarga que falha de novo) volta a aparecer mesmo depois de dispensado o anterior.
export function useErroCarga(consultas: UseQueryResult[], padrao: string) {
  const [dispensadoEm, setDispensadoEm] = useState(0);
  const comErro = consultas.find((c) => c.isError);
  const erroEm = Math.max(...consultas.map((c) => c.errorUpdatedAt));
  const erro =
    comErro && erroEm !== dispensadoEm ? (comErro.error instanceof Error ? comErro.error.message : padrao) : null;
  return { erro, dispensar: () => setDispensadoEm(erroEm) };
}
