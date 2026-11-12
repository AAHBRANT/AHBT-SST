import type { ReactNode } from 'react';
import { useTipografia, type Tom } from '@ui';
import { mergeClasses, useEstilosCameraIp } from './estilosCameraIp';

type TomMarca = Extract<Tom, 'ok' | 'atencao' | 'neutro' | 'info'>;

// Marca de estado com forma + cor (círculo cheio, quadrado, círculo vazado, losango): não depende só da cor,
// para funcionar com sol forte, luva e tela suja. Câmeras de tablet seguem com a marca antiga do Mosaico.
export function MarcaEstado({ tom, children }: { tom: Tom; children: ReactNode }) {
  const e = useEstilosCameraIp();
  const tipo = useTipografia();
  const t: TomMarca = tom === 'ok' || tom === 'atencao' || tom === 'info' ? tom : 'neutro';
  const forma = { ok: e.formaOk, atencao: e.formaAtencao, neutro: e.formaNeutro, info: e.formaInfo }[t];
  const cor = { ok: e.tomOk, atencao: e.tomAtencao, neutro: e.tomNeutro, info: e.tomInfo }[t];
  return (
    <span className={mergeClasses(e.estado, tipo.legenda, cor)}>
      <span aria-hidden="true" className={mergeClasses(e.forma, forma)} />
      {children}
    </span>
  );
}

export interface OpcaoCartaoProps {
  ativa: boolean;
  bloqueada?: boolean;
  titulo: ReactNode;
  descricao: ReactNode;
  aoEscolher: () => void;
}

// Opção em cartão com alvo de toque amplo (radio grande). Bloqueada = visível, mas não escolhível.
export function OpcaoCartao({ ativa, bloqueada, titulo, descricao, aoEscolher }: OpcaoCartaoProps) {
  const e = useEstilosCameraIp();
  return (
    <button
      type="button"
      role="radio"
      aria-checked={ativa}
      aria-disabled={bloqueada || undefined}
      disabled={bloqueada}
      className={mergeClasses(e.opc, ativa && e.opcAtiva, bloqueada && e.opcBloqueada)}
      onClick={aoEscolher}
    >
      <span aria-hidden="true" className={mergeClasses(e.radio, ativa && e.radioAtivo)} />
      <span>
        <span className={e.opcTitulo}>{titulo}</span>
        <span className={e.opcDescricao}>{descricao}</span>
      </span>
    </button>
  );
}
