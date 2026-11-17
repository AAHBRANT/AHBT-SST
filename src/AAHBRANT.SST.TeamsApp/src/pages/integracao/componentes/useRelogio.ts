import { useEffect, useState } from 'react';

// Relógio da tela "Realizar integração" (16/11/2026): devolve `Date.now()` e atualiza a cada `passoMs`, no
// instante exato de cada horário em `alvosMs` (ex.: o fim do período de carga, para o Avançar liberar na hora) e
// quando a aba volta a ficar visível (timers de aba em segundo plano atrasam). A conta do que falta é sempre
// por horário absoluto (helpersInstrutor.ts); este hook só dispara a re-renderização.
export function useRelogio(passoMs: number, alvosMs: ReadonlyArray<number | null | undefined> = []): number {
  const [agora, setAgora] = useState(() => Date.now());
  const chave = alvosMs.filter((v): v is number => typeof v === 'number' && Number.isFinite(v)).join('|');

  useEffect(() => {
    const atualizar = () => setAgora(Date.now());
    const id = window.setInterval(atualizar, passoMs);
    const aoVoltar = () => {
      if (document.visibilityState === 'visible') atualizar();
    };
    document.addEventListener('visibilitychange', aoVoltar);
    // Um timeout por horário futuro (teto de ~24 dias do setTimeout; além disso o intervalo cobre).
    const agoraMs = Date.now();
    const timeouts = (chave ? chave.split('|').map(Number) : [])
      .filter((alvo) => alvo > agoraMs && alvo - agoraMs < 2_000_000_000)
      .map((alvo) => window.setTimeout(atualizar, alvo - agoraMs + 300));
    return () => {
      window.clearInterval(id);
      document.removeEventListener('visibilitychange', aoVoltar);
      timeouts.forEach((t) => window.clearTimeout(t));
    };
  }, [passoMs, chave]);

  return agora;
}
