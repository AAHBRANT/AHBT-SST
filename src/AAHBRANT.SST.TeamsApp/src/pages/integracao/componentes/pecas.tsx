import type { ReactNode } from 'react';
import { Checkmark16Regular } from '@fluentui/react-icons';
import { mergeClasses, useEstilosIntegracao } from './estilos';
import { iniciais } from './helpers';

// Peças pequenas da tela "Realizar integração": número redondo do título de seção e avatar com
// iniciais. Ficam aqui porque só esta tela usa o número redondo do mockup v4.

export function TituloSecao({ numero, estado, children }: { numero: ReactNode; estado?: 'feito' | 'atual' | 'neutro'; children: ReactNode }) {
  const e = useEstilosIntegracao();
  return (
    <span className={e.secTitulo}>
      <span className={mergeClasses(e.numero, estado === 'feito' && e.numeroFeito, estado === 'atual' && e.numeroAtual)} aria-hidden="true">
        {estado === 'feito' ? <Checkmark16Regular /> : numero}
      </span>
      <span>{children}</span>
    </span>
  );
}

export function AvatarIniciais({ nome, pequeno, selecionado }: { nome: string; pequeno?: boolean; selecionado?: boolean }) {
  const e = useEstilosIntegracao();
  return (
    <span className={mergeClasses(e.avatar, pequeno && e.avatarPequeno, selecionado && e.avatarSel)} aria-hidden="true">
      {iniciais(nome)}
    </span>
  );
}
