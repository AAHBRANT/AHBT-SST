import { useCallback, useState } from 'react';

// Obra escolhida nas telas da Gestão de SST (07/11/2026): a tela já abre numa obra (a última escolhida
// nesta sessão do navegador ou, na falta dela, a primeira em andamento) e a escolha vale ao trocar de
// aba, para não ter de selecionar de novo em cada uma.
const CHAVE = 'sst:obraSelecionada';
const EM_ANDAMENTO = 2;

export function lerLembrada(): string {
  try {
    return sessionStorage.getItem(CHAVE) ?? '';
  } catch {
    return '';
  }
}

export function gravar(id: string) {
  try {
    if (id) sessionStorage.setItem(CHAVE, id);
  } catch {
    // Sem armazenamento (modo privado): só não lembra.
  }
}

export function escolherObraInicial(lista: { id: string; status?: number }[]): string {
  const lembrada = lerLembrada();
  if (lembrada && lista.some((o) => o.id === lembrada)) return lembrada;
  return (lista.find((o) => o.status === EM_ANDAMENTO) ?? lista[0])?.id ?? '';
}

export function useObraLembrada(): [string, (id: string) => void] {
  const [obraId, setObraIdState] = useState('');
  const setObraId = useCallback((id: string) => {
    setObraIdState(id);
    gravar(id);
  }, []);
  return [obraId, setObraId];
}
