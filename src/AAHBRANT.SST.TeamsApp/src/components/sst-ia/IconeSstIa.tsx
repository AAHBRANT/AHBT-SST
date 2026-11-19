import type { SVGProps } from 'react';

// Ícone do SST IA (opção A aprovada em 18/11/2026): capacete de obra com headset — "alguém da
// segurança pronto para atender". Recortado e com traço 1.85 para ter o mesmo peso visual do
// capacete de "Operação" no menu lateral.
export function IconeSstIa(props: SVGProps<SVGSVGElement>) {
  return (
    <svg
      viewBox="2.8 4 18.4 18.2"
      width="1em"
      height="1em"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.85"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      {...props}
    >
      <path d="M5.5 11.5a6.5 6.5 0 0 1 13 0" />
      <path d="M12 5v3.2" />
      <path d="M3.8 11.5h16.4" />
      <rect x="3.6" y="13.2" width="3" height="4.6" rx="1.2" />
      <rect x="17.4" y="13.2" width="3" height="4.6" rx="1.2" />
      <path d="M18.9 17.8c0 1.9-1.6 3.2-4 3.2h-1.4" />
      <circle cx="12.4" cy="21" r=".9" fill="currentColor" stroke="none" />
    </svg>
  );
}
