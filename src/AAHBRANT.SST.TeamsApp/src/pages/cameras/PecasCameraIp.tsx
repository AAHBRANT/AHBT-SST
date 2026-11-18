import type { ReactNode } from 'react';
import { Dismiss16Regular } from '@fluentui/react-icons';
import { useTipografia, type Tom } from '@ui';
import { ParteImagemCamera } from '../../lib/api';
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
  // Desenho no fim do cartão (ex.: qual parte da imagem a opção usa).
  lateral?: ReactNode;
}

// Opção em cartão com alvo de toque amplo (radio grande). Bloqueada = visível, mas não escolhível.
export function OpcaoCartao({ ativa, bloqueada, titulo, descricao, aoEscolher, lateral }: OpcaoCartaoProps) {
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
      {lateral}
    </button>
  );
}

// Desenho de uma imagem de duas vistas empilhadas, com a parte usada preenchida (inteira, só a de cima ou só a de baixo).
export function GlifoParteImagem({ parte, ativa }: { parte: number; ativa: boolean }) {
  const e = useEstilosCameraIp();
  const cima = parte !== ParteImagemCamera.MetadeInferior ? 'currentColor' : 'none';
  const baixo = parte !== ParteImagemCamera.MetadeSuperior ? 'currentColor' : 'none';
  return (
    <svg
      className={mergeClasses(e.glifo, ativa && e.glifoAtivo)}
      width="28"
      height="32"
      viewBox="0 0 28 32"
      aria-hidden="true"
    >
      <rect x="1.5" y="1.5" width="25" height="29" rx="3" fill="none" stroke="currentColor" strokeWidth="2" />
      <rect x="4" y="4" width="20" height="11" rx="1" fill={cima} opacity=".9" />
      <rect x="4" y="17" width="20" height="11" rx="1" fill={baixo} opacity=".9" />
    </svg>
  );
}

// Selo da lista e do Mosaico: só aparece quando a câmera está sem aviso no Telegram (interruptor desligado).
export function SeloSemTelegram() {
  const e = useEstilosCameraIp();
  return (
    <span className={e.seloSemTelegram}>
      <Dismiss16Regular aria-hidden="true" />
      Sem aviso no Telegram
    </span>
  );
}
