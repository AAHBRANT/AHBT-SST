import type { ReactNode, SVGProps } from 'react';

// Ícones próprios do rail (redesenho da sidebar, 27/11/2026, mockup
// docs/mockups/sidebar-icones-movimento-2026-11-27.html). Todos na mesma grade 24×24, traço 1.7 e
// cantos arredondados — o capacete antigo usava viewBox não quadrado e 1em, e ficava "fora do
// esquadro" ao lado dos ícones do Fluent. Os trechos com a classe `f` ganham um preenchimento
// suave quando o item está ativo (ver `.gs-rail-item-ativo` em AppShell).
function IconeRail({ children, ...props }: SVGProps<SVGSVGElement> & { children: ReactNode }) {
  return (
    <svg
      viewBox="0 0 24 24"
      width={22}
      height={22}
      fill="none"
      stroke="currentColor"
      strokeWidth={1.7}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      className="gs-rail-icone"
      {...props}
    >
      {children}
    </svg>
  );
}

type P = SVGProps<SVGSVGElement>;

export function IconeInicio(props: P) {
  return (
    <IconeRail {...props}>
      <path className="f" d="M4 10.4 12 4l8 6.4V19a1 1 0 0 1-1 1h-4.5v-5.5h-5V20H5a1 1 0 0 1-1-1z" />
    </IconeRail>
  );
}

export function IconeGestaoSst(props: P) {
  return (
    <IconeRail {...props}>
      <path className="f" d="M12 3.2 19 6v5.6c0 4.3-2.9 7.8-7 9.2-4.1-1.4-7-4.9-7-9.2V6z" />
      <path d="m8.9 12 2.2 2.2 4-4.2" />
    </IconeRail>
  );
}

export function IconeCapacete(props: P) {
  return (
    <IconeRail {...props}>
      <path className="f" d="M4.5 15.5a7.5 7.5 0 0 1 15 0z" />
      <path d="M10 8.3v7.2M14 8.3v7.2" />
      <path className="f" d="M2.8 15.5h18.4v1.6a1.6 1.6 0 0 1-1.6 1.6H4.4a1.6 1.6 0 0 1-1.6-1.6z" />
    </IconeRail>
  );
}

export function IconePessoas(props: P) {
  return (
    <IconeRail {...props}>
      <circle className="f" cx="9" cy="8.2" r="3.2" />
      <path className="f" d="M3.5 19.5a5.5 5.5 0 0 1 11 0z" />
      <circle cx="16.6" cy="9.2" r="2.5" />
      <path d="M15.8 14.2a4.6 4.6 0 0 1 4.8 4.6" />
    </IconeRail>
  );
}

export function IconeOcorrencia(props: P) {
  return (
    <IconeRail {...props}>
      <path className="f" d="M10.3 4.9a2 2 0 0 1 3.4 0l7 12.1a2 2 0 0 1-1.7 3H5a2 2 0 0 1-1.7-3z" />
      <path d="M12 9.6v4M12 16.8h.01" />
    </IconeRail>
  );
}

export function IconeCracha(props: P) {
  return (
    <IconeRail {...props}>
      <rect className="f" x="3.5" y="6.5" width="17" height="13" rx="2" />
      <path d="M9.5 6.5V5a1 1 0 0 1 1-1h3a1 1 0 0 1 1 1v1.5" />
      <circle cx="8.8" cy="12.2" r="1.9" />
      <path d="M5.9 16.8a3.2 3.2 0 0 1 5.8 0M14.5 11.5h3M14.5 14.5h3" />
    </IconeRail>
  );
}

export function IconeCalendario(props: P) {
  return (
    <IconeRail {...props}>
      <rect className="f" x="4" y="5.5" width="16" height="14.5" rx="2" />
      <path d="M4 10h16M8.5 3.5v3.5M15.5 3.5v3.5M8 13.5h.01M12 13.5h.01M16 13.5h.01M8 16.8h.01M12 16.8h.01" />
    </IconeRail>
  );
}

export function IconeCamera(props: P) {
  return (
    <IconeRail {...props}>
      <rect className="f" x="3" y="6" width="13" height="7" rx="1.6" />
      <path d="m16 8 4.5-1.8v6.6L16 11M9 13v2.5a1 1 0 0 1-1 1H4.5M4.5 14.5V20M6 9.5h.01" />
    </IconeRail>
  );
}

export function IconeIaBrilho(props: P) {
  return (
    <IconeRail {...props}>
      <path className="f" d="m11 4 1.7 4.8L17.5 10.5l-4.8 1.7L11 17l-1.7-4.8L4.5 10.5l4.8-1.7z" />
      <path d="m18 14.5.7 1.8 1.8.7-1.8.7-.7 1.8-.7-1.8-1.8-.7 1.8-.7z" />
    </IconeRail>
  );
}

export function IconeAdministracao(props: P) {
  return (
    <IconeRail {...props}>
      <path d="M4 7h8.5M16.5 7H20M4 12h2.5M10.5 12H20M4 17h10.5M18.5 17H20" />
      <circle className="f" cx="14.5" cy="7" r="2" />
      <circle className="f" cx="8.5" cy="12" r="2" />
      <circle className="f" cx="16.5" cy="17" r="2" />
    </IconeRail>
  );
}
