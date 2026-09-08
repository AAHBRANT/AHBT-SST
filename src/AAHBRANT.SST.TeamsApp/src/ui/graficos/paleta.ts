import { useEffect, useState } from 'react';

// Paleta para Recharts (spec §1.6): lê as CSS custom properties em tempo de execução, porque SVG
// de gráfico não aceita var() em fill/stroke. Observa data-theme na raiz para acompanhar a troca de tema.
function ler(nome: string): string {
  return getComputedStyle(document.documentElement).getPropertyValue(nome).trim();
}

function resolver() {
  const ok = ler('--sst-status-ok-tinta'), atencao = ler('--sst-status-atencao-tinta'), alerta = ler('--sst-status-alerta-tinta');
  const info = ler('--sst-status-info-tinta'), neutro = ler('--sst-status-neutro-tinta');
  const chrome = ler('--sst-chrome-ativo-fundo'), marca = ler('--sst-color-primary');
  return { ok, atencao, alerta, info, neutro, chrome, marca, serie: [chrome, info, atencao, marca, ok, neutro] };
}

export type PaletaGraficos = ReturnType<typeof resolver>;

export function usePaletaGraficos(): PaletaGraficos {
  const [paleta, setPaleta] = useState<PaletaGraficos>(resolver);
  useEffect(() => {
    const obs = new MutationObserver(() => setPaleta(resolver()));
    obs.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
    return () => obs.disconnect();
  }, []);
  return paleta;
}
