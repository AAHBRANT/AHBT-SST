import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { api, type UsuarioLogado } from './api';

interface UsuarioLogadoContextValue {
  usuario: UsuarioLogado | null;
  // Enquanto carrega, nenhuma decisão de permissão foi tomada ainda: a tela deve esperar em vez de
  // assumir "não pode". Sem isto, o botão de excluir piscaria na tela a cada navegação.
  carregando: boolean;
}

const UsuarioLogadoContext = createContext<UsuarioLogadoContextValue>({ usuario: null, carregando: true });

function rotaAtualEhPublica(): boolean {
  return window.location.hash.startsWith('#/validar/') || window.location.hash.startsWith('#/p/');
}

// Quem está logado e o que essa pessoa pode fazer (23/09). Antes disto o frontend não tinha essa
// informação em lugar nenhum — toda checagem de permissão vivia só no servidor, e a tela mostrava
// as mesmas ações para todo mundo. Carregado uma vez, no topo da árvore: é uma chamada por sessão,
// não por tela.
//
// O servidor continua sendo quem decide de verdade (ver PoliticasAutorizacao): isto aqui só evita
// oferecer um botão que resultaria em recusa.
export function UsuarioLogadoProvider({ children }: { children: ReactNode }) {
  const [usuario, setUsuario] = useState<UsuarioLogado | null>(null);
  const [carregando, setCarregando] = useState(true);

  useEffect(() => {
    if (rotaAtualEhPublica()) {
      setUsuario(null);
      setCarregando(false);
      return;
    }

    let cancelado = false;
    api.usuarios
      .eu()
      .then((u) => {
        if (!cancelado) setUsuario(u);
      })
      .catch(() => {
        // Falhou ao descobrir quem é: trata como sem privilégio. Esconder o botão de excluir é o
        // lado seguro do erro — o servidor recusaria a exclusão de qualquer forma.
        if (!cancelado) setUsuario(null);
      })
      .finally(() => {
        if (!cancelado) setCarregando(false);
      });
    return () => {
      cancelado = true;
    };
  }, []);

  return <UsuarioLogadoContext.Provider value={{ usuario, carregando }}>{children}</UsuarioLogadoContext.Provider>;
}

export function useUsuarioLogado() {
  return useContext(UsuarioLogadoContext);
}

// Atalho para o caso mais comum: mostrar ou não uma ação restrita a Administrador. Retorna false
// enquanto carrega — o botão aparece quando a resposta chega, em vez de sumir depois de aparecer.
export function useSouAdministrador(): boolean {
  const { usuario } = useUsuarioLogado();
  return usuario?.ehAdministrador === true;
}
