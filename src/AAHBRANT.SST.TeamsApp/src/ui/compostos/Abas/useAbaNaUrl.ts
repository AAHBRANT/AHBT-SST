import { useCallback } from 'react';
import { useSearchParams } from 'react-router-dom';

// Aba sincronizada com a URL nos dois sentidos (spec §3): hoje as páginas-pilar só leem ?secao= na
// montagem — voltar do navegador e F5 perdem a aba. Este hook lê e escreve o parâmetro; a URL é a
// fonte da verdade. Usa replace para não poluir o histórico a cada clique.
export function useAbaNaUrl<T extends string>(param: string, validas: readonly T[], padrao: T): [T, (v: T) => void] {
  const [params, setParams] = useSearchParams();
  const bruto = params.get(param);
  const atual = (validas as readonly string[]).includes(bruto ?? '') ? (bruto as T) : padrao;
  const definir = useCallback((v: T) => {
    setParams((p) => { const novo = new URLSearchParams(p); novo.set(param, v); return novo; }, { replace: true });
  }, [param, setParams]);
  return [atual, definir];
}
