import type { CSSProperties } from 'react';
import './marca.css';

// Logo oficial da AAHBRANT (public/marca), mesmo padrão do cabeçalho do G-QUALI.
// O brilho usa a própria logo como máscara: nada é recolorido nem redesenhado.

const prefixo = import.meta.env.BASE_URL.endsWith('/') ? import.meta.env.BASE_URL : `${import.meta.env.BASE_URL}/`;
const URL_LOGO = `${prefixo}marca/aahbrant-positiva.png`;

// altura: px fixos também no próprio <img> (WebView do Teams não resolve altura em %).
export function LogoAahbrant({ altura }: { altura: number }) {
  const estilo = { '--gs-logo-url': `url("${URL_LOGO}")`, '--gs-logo-altura': `${altura}px` } as CSSProperties;
  return (
    <span className="gs-logo" style={estilo}>
      <img src={URL_LOGO} alt="AAHBRANT Engenharia & Construções" draggable={false} style={{ height: altura, width: 'auto' }} />
      <span className="gs-logo-brilho" aria-hidden />
    </span>
  );
}
