import { useMemo, useState } from 'react';
import { Combobox, Option, makeStyles } from '@fluentui/react-components';
import { designTokens } from '../../tokens/tokens';

const useStyles = makeStyles({
  // minWidth 0 na raiz não basta: o <input> interno do Combobox tem largura intrínseca própria
  // (~191px) e, numa faixa mais estreita que ela, empurrava a seta de expandir para fora do campo —
  // visível na coluna "Responsável" (span 3) da NaoConformidadeDetalhePage, piloto 2.
  root: { width: '100%', minWidth: 0,
    '& .fui-Combobox__input': { minWidth: 0, width: '100%' } },
  descricao: { display: 'block', color: designTokens.colorNeutralMedium, fontSize: '11px', lineHeight: '14px', fontWeight: 600 },
});

export interface OpcaoSeletor { id: string; rotulo: string; descricao?: string }

export interface SeletorPesquisavelProps {
  /** Memoize em listas grandes (ex.: `useMemo` sobre trabalhadores). */
  opcoes: OpcaoSeletor[];
  valor: string;
  aoMudar: (id: string) => void;
  placeholder?: string;
  vazio?: string;
  disabled?: boolean;
  /**
   * Use só quando o seletor NÃO estiver dentro de um `<Field label>`; com Field, o rótulo já vem
   * do contexto.
   */
  'aria-label'?: string;
}

function normalizar(s: string) {
  return s.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase();
}

// Seletor com busca por texto (spec §3). Wrapper de Combobox do Fluent (0 usos até aqui). Substitui
// <Select> quando a lista passa de ~15 itens — trabalhadores, EPIs, cursos, obras. Ignora acentos.
export function SeletorPesquisavel({ opcoes, valor, aoMudar, placeholder, vazio = 'Nenhum resultado', disabled, 'aria-label': ariaLabel }: SeletorPesquisavelProps) {
  const e = useStyles();
  const [busca, setBusca] = useState('');
  const selecionada = opcoes.find((o) => o.id === valor);
  const filtradas = useMemo(() => {
    const q = normalizar(busca.trim());
    if (!q) return opcoes;
    return opcoes.filter((o) => normalizar(`${o.rotulo} ${o.descricao ?? ''}`).includes(q));
  }, [busca, opcoes]);

  return (
    <Combobox
      className={e.root}
      aria-label={ariaLabel}
      placeholder={placeholder}
      disabled={disabled}
      freeform
      value={busca || selecionada?.rotulo || ''}
      selectedOptions={valor ? [valor] : []}
      onChange={(ev) => setBusca(ev.target.value)}
      onOptionSelect={(_, d) => { aoMudar(d.optionValue ?? ''); setBusca(''); }}
      onOpenChange={(_, d) => { if (!d.open) setBusca(''); }}
    >
      {filtradas.length === 0 && <Option disabled value="__vazio" text={vazio}>{vazio}</Option>}
      {filtradas.map((o) => (
        <Option key={o.id} value={o.id} text={o.rotulo}>
          <span>{o.rotulo}{o.descricao && <span className={e.descricao}>{o.descricao}</span>}</span>
        </Option>
      ))}
    </Combobox>
  );
}
