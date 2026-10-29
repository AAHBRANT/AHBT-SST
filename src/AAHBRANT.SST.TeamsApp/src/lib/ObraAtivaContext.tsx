import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';

// Obra ativa do sistema inteiro (pedido do usuário, 28/10/2026): o usuário escolhe a obra uma vez,
// no cabeçalho, e ela vale para o Início e para todos os painéis e telas que filtram por obra. A
// escolha fica lembrada neste navegador para o próximo acesso. Vazio ('') = todas as obras.
//
// Antes cada tela tinha o próprio filtro local (useState('')), que voltava para "Todas as obras" a
// cada troca de página — e o usuário nunca sabia em qual obra estava.

const CHAVE = 'sst.obraAtiva';

interface ObraAtiva {
  id: string;
  nome: string;
}

interface ObraAtivaContexto {
  /** Id da obra ativa; '' quando são todas as obras. */
  obraAtivaId: string;
  /** Nome da obra ativa, para mostrar no cabeçalho sem esperar a lista de obras. */
  obraAtivaNome: string;
  definirObraAtiva: (id: string, nome?: string) => void;
}

const Contexto = createContext<ObraAtivaContexto | null>(null);

function lerSalva(): ObraAtiva {
  try {
    const bruto = window.localStorage.getItem(CHAVE);
    if (bruto) {
      const salvo = JSON.parse(bruto) as Partial<ObraAtiva>;
      if (typeof salvo.id === 'string') return { id: salvo.id, nome: typeof salvo.nome === 'string' ? salvo.nome : '' };
    }
  } catch {
    // localStorage indisponível ou valor corrompido: começa em "Todas as obras".
  }
  return { id: '', nome: '' };
}

export function ObraAtivaProvider({ children }: { children: ReactNode }) {
  const [obra, setObra] = useState<ObraAtiva>(lerSalva);

  const definirObraAtiva = useCallback((id: string, nome = '') => {
    setObra((atual) => {
      const nova = { id, nome: id ? nome || (atual.id === id ? atual.nome : '') : '' };
      try {
        window.localStorage.setItem(CHAVE, JSON.stringify(nova));
      } catch {
        // Segue só em memória se o navegador não deixar gravar.
      }
      return nova;
    });
  }, []);

  const valor = useMemo(
    () => ({ obraAtivaId: obra.id, obraAtivaNome: obra.nome, definirObraAtiva }),
    [obra, definirObraAtiva],
  );
  return <Contexto.Provider value={valor}>{children}</Contexto.Provider>;
}

export function useObraAtiva(): ObraAtivaContexto {
  const contexto = useContext(Contexto);
  if (!contexto) throw new Error('useObraAtiva precisa do ObraAtivaProvider (App.tsx).');
  return contexto;
}

/**
 * Filtro de obra de uma tela, ligado à obra ativa do sistema: mesma assinatura de
 * `useState('')`, para trocar `const [obraId, setObraId] = useState('')` sem mexer no resto da tela.
 * Trocar a obra numa tela troca para o sistema todo.
 */
export function useFiltroObra(): [string, (id: string) => void] {
  const { obraAtivaId, definirObraAtiva } = useObraAtiva();
  return [obraAtivaId, definirObraAtiva];
}
