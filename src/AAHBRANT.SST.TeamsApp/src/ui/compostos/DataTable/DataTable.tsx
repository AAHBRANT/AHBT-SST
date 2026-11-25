import { Fragment, useRef, useState, type PointerEvent as ReactPointerEvent, type ReactNode } from 'react';
import { mergeClasses } from '@fluentui/react-components';
import { ChevronRight16Regular } from '@fluentui/react-icons';
import { motion } from 'framer-motion';
import { EstadoVazio, type EstadoVazioProps } from '../../primitivos/EstadoVazio/EstadoVazio';
import { Carregando } from '../../primitivos/Carregando/Carregando';
import { molas } from '../../tokens/movimento';
import { tokensUi, type Tom } from '../../tokens/tokens';
import { useDataTableStyles } from './DataTable.styles';

export interface Coluna<T> {
  chave: string;
  rotulo: ReactNode;
  largura?: string;
  alinhar?: 'esquerda' | 'direita' | 'centro';
  render?: (linha: T) => ReactNode;
}

export interface DataTableProps<T> {
  colunas: Coluna<T>[];
  linhas: T[];
  chaveLinha: (linha: T) => string;
  carregando?: boolean;
  vazio?: EstadoVazioProps;
  densidade?: 'confortavel' | 'compacta';
  aoClicarLinha?: (linha: T) => void;
  acoesLinha?: (linha: T) => ReactNode;
  expansivel?: { aberta: (linha: T) => boolean; render: (linha: T) => ReactNode };
  cabecalhoFixo?: boolean;
  /** Cor da faixa na lateral esquerda da linha (situação do registro). Sem retorno, sem faixa. */
  tomLinha?: (linha: T) => Tom | null | undefined;
  'aria-label'?: string;
}

// Atraso da entrada em cascata: 28 ms por linha, no máximo 0,3 s — listas longas não esperam.
const ATRASO_POR_LINHA_MS = 28;
const ATRASO_MAXIMO_MS = 300;

interface Realce { y: number; altura: number; visivel: boolean }

// Tabela do sistema (spec §3): linhas planas com divisória, realce que desliza sob o ponteiro,
// faixa de situação, estados de carregando/vazio embutidos, densidade, ações por linha agrupadas,
// linha expansível (com chevron ▸/▾ na primeira coluna, spec §4.5). Visual aprovado em 25/11/2026
// (docs/mockups/listas-gsst-2026-11-25.html).
export function DataTable<T>({ colunas, linhas, chaveLinha, carregando, vazio, densidade = 'confortavel', aoClicarLinha, acoesLinha, expansivel, cabecalhoFixo, tomLinha, 'aria-label': ariaLabel }: DataTableProps<T>) {
  const e = useDataTableStyles();
  const wrapRef = useRef<HTMLDivElement>(null);
  const [realce, setRealce] = useState<Realce>({ y: 0, altura: 0, visivel: false });
  const compacta = densidade === 'compacta';
  const totalColunas = colunas.length + (acoesLinha ? 1 : 0);

  if (carregando) return <Carregando variante="lista" linhas={5} />;
  if (linhas.length === 0) return <EstadoVazio titulo="Nada por aqui" {...vazio} />;

  function celula(linha: T, col: Coluna<T>) {
    if (col.render) return col.render(linha);
    const v = (linha as Record<string, unknown>)[col.chave];
    return v === null || v === undefined ? '' : String(v);
  }

  // Leva o realce até a linha sob o ponteiro (só linhas de dados; a área expandida não acende).
  function moverRealce(ev: ReactPointerEvent<HTMLTableSectionElement>) {
    const tr = (ev.target as HTMLElement).closest('tr[data-linha]');
    const wrap = wrapRef.current;
    if (!tr || !wrap) return;
    const r = tr.getBoundingClientRect();
    const w = wrap.getBoundingClientRect();
    const y = r.top - w.top + wrap.scrollTop;
    setRealce((atual) => (atual.visivel && atual.y === y && atual.altura === r.height ? atual : { y, altura: r.height, visivel: true }));
  }

  return (
    <div ref={wrapRef} className={mergeClasses(e.wrap, cabecalhoFixo && e.wrapCabecalhoFixo)}>
      <motion.div
        aria-hidden="true"
        className={e.realce}
        initial={false}
        animate={{ y: realce.y, height: realce.altura, opacity: realce.visivel ? 1 : 0 }}
        transition={{ y: molas.seguir, height: molas.seguir, opacity: { duration: 0.15 } }}
      />
      <table className={mergeClasses(e.tabela, compacta && e.compacta, cabecalhoFixo && e.cabecalhoFixo)} aria-label={ariaLabel}>
        <thead>
          <tr>
            {colunas.map((c, i) => <th key={`${i}-${c.chave}`} className={mergeClasses(e.th, i === 0 && e.tdPrimeira, c.alinhar === 'direita' && e.direita, c.alinhar === 'centro' && e.centro)} style={c.largura ? { width: c.largura } : undefined}>{c.rotulo}</th>)}
            {acoesLinha && <th className={mergeClasses(e.th, e.direita)}><span style={{ position: 'absolute', width: 1, height: 1, overflow: 'hidden', clip: 'rect(0 0 0 0)' }}>Ações</span></th>}
          </tr>
        </thead>
        <tbody onPointerMove={moverRealce} onPointerLeave={() => setRealce((atual) => ({ ...atual, visivel: false }))}>
          {linhas.map((linha, indice) => {
            const chave = chaveLinha(linha);
            const aberta = expansivel?.aberta(linha) ?? false;
            const clicavel = !!aoClicarLinha;
            const tom = tomLinha?.(linha);
            return (
              <Fragment key={chave}>
                <tr
                  data-linha=""
                  className={mergeClasses(e.tr, clicavel && e.trClicavel)}
                  style={{ animationDelay: `${Math.min(indice * ATRASO_POR_LINHA_MS, ATRASO_MAXIMO_MS)}ms` }}
                  onClick={clicavel ? () => aoClicarLinha(linha) : undefined}
                  aria-expanded={expansivel ? aberta : undefined}
                  tabIndex={clicavel ? 0 : undefined}
                  onKeyDown={clicavel ? (ev) => { if (ev.key === 'Enter' || ev.key === ' ') { ev.preventDefault(); aoClicarLinha(linha); } } : undefined}
                >
                  {colunas.map((c, i) => (
                    <td key={`${i}-${c.chave}`} className={mergeClasses(e.td, compacta && e.tdCompacta, i === 0 && e.tdPrimeira, i === colunas.length - 1 && !acoesLinha && e.tdUltima, c.alinhar === 'direita' && e.direita, c.alinhar === 'centro' && e.centro)}>
                      {i === 0 && tom && <span aria-hidden="true" className={e.faixa} style={{ backgroundColor: tokensUi.status[tom].tinta }} />}
                      {i === 0 && expansivel ? (
                        <span className={e.celulaComSeta}>
                          <ChevronRight16Regular aria-hidden="true" className={mergeClasses(e.seta, aberta && e.setaAberta)} />
                          <span>{celula(linha, c)}</span>
                        </span>
                      ) : (
                        celula(linha, c)
                      )}
                    </td>
                  ))}
                  {acoesLinha && (
                    <td className={mergeClasses(e.td, compacta && e.tdCompacta, e.tdUltima, e.direita)} onClick={(ev) => ev.stopPropagation()} onKeyDown={(ev) => ev.stopPropagation()}>
                      <div className={e.acoes}>{acoesLinha(linha)}</div>
                    </td>
                  )}
                </tr>
                {expansivel && aberta && (
                  <tr>
                    <td colSpan={totalColunas} style={{ padding: 0, border: 0 }}>
                      <div className={e.expandida}>{expansivel.render(linha)}</div>
                    </td>
                  </tr>
                )}
              </Fragment>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
