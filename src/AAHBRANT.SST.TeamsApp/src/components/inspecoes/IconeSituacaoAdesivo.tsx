import type { SituacaoAdesivo } from '../../lib/api';

export function IconeSituacaoAdesivo({ situacao, tamanho }: { situacao: SituacaoAdesivo; tamanho: number | string }) {
  return (
    <svg
      viewBox="0 0 24 24"
      width={tamanho}
      height={tamanho}
      fill="none"
      stroke="#fff"
      strokeWidth="2.4"
      aria-hidden="true"
      style={{ flex: 'none', display: 'block' }}
    >
      <circle cx="12" cy="12" r="10" />
      {situacao === 'NaoLiberado' ? <path d="M8 8l8 8M16 8l-8 8" /> : <path d="M7 12.5l3.2 3.2L17 9" />}
    </svg>
  );
}
