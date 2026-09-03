import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';

export type ModoTema = 'light' | 'dark';

const CHAVE_MODO_TEMA = 'sst.modoTema';

interface ThemeModeContextValue {
  modo: ModoTema;
  alternarModo: () => void;
}

const ThemeModeContext = createContext<ThemeModeContextValue | null>(null);

// Botão de dark/light mode (pedido do usuário, 02/09) — modo escuro é o padrão do app desde a
// reformulação de navegação/tema desta mesma data, mas o usuário pode alternar pro claro (usado
// antes, ver histórico de theme.ts) e a escolha persiste entre sessões, mesmo padrão das outras
// preferências do rail (ver CHAVE_RAIL_EXPANDIDO em AppShell.tsx).
export function ThemeModeProvider({ children }: { children: ReactNode }) {
  const [modo, setModo] = useState<ModoTema>(() => {
    const salvo = localStorage.getItem(CHAVE_MODO_TEMA);
    const inicial = salvo === 'light' ? 'light' : 'dark';
    // Aplica já no initializer (antes da 1ª pintura), não só no useEffect abaixo — senão a página
    // pisca no tema claro (default do CSS) por um instante antes do React montar.
    document.documentElement.setAttribute('data-theme', inicial);
    return inicial;
  });

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', modo);
    localStorage.setItem(CHAVE_MODO_TEMA, modo);
  }, [modo]);

  function alternarModo() {
    setModo((atual) => (atual === 'dark' ? 'light' : 'dark'));
  }

  return <ThemeModeContext.Provider value={{ modo, alternarModo }}>{children}</ThemeModeContext.Provider>;
}

export function useThemeMode(): ThemeModeContextValue {
  const contexto = useContext(ThemeModeContext);
  if (!contexto) {
    throw new Error('useThemeMode precisa ser usado dentro de um ThemeModeProvider.');
  }
  return contexto;
}
