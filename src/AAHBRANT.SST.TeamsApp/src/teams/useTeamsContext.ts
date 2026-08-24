import { useEffect, useState } from 'react';
import { app } from '@microsoft/teams-js';

interface TeamsContextState {
  carregando: boolean;
  dentroDoTeams: boolean;
  contexto: app.Context | null;
}

// Inicializa o SDK do Teams quando a Tab roda dentro do cliente Teams (iframe).
// Fora do Teams (dev local no navegador), app.initialize() rejeita e caímos no modo standalone,
// permitindo desenvolver/testar a UI sem depender de sideload no Teams.
export function useTeamsContext(): TeamsContextState {
  const [estado, setEstado] = useState<TeamsContextState>({
    carregando: true,
    dentroDoTeams: false,
    contexto: null,
  });

  useEffect(() => {
    let cancelado = false;

    app
      .initialize()
      .then(() => app.getContext())
      .then((contexto) => {
        if (!cancelado) {
          setEstado({ carregando: false, dentroDoTeams: true, contexto });
        }
      })
      .catch(() => {
        if (!cancelado) {
          setEstado({ carregando: false, dentroDoTeams: false, contexto: null });
        }
      });

    return () => {
      cancelado = true;
    };
  }, []);

  return estado;
}
