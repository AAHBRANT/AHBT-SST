import { Fragment, type ReactNode } from 'react';
import { mergeClasses } from '@fluentui/react-components';
import { EstadoVazio, type EstadoVazioProps } from '../../primitivos/EstadoVazio/EstadoVazio';
import { Carregando } from '../../primitivos/Carregando/Carregando';
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
  'aria-label'?: string;
}

// Tabela do sistema (spec §3): linhas-como-cartão, estados de carregando/vazio embutidos, densidade,
// ações por linha, linha expansível. Substitui os 63 usos de <Table> cru do Fluent.
export function DataTable<T>({ colunas, linhas, chaveLinha, carregando, vazio, densidade = 'confortavel', aoClicarLinha, acoesLinha, expansivel, cabecalhoFixo, 'aria-label': ariaLabel }: DataTableProps<T>) {
  const e = useDataTableStyles();
  const compacta = densidade === 'compacta';
  const totalColunas = colunas.length + (acoesLinha ? 1 : 0);

  if (carregando) return <Carregando variante="lista" linhas={5} />;
  if (linhas.length === 0) return <EstadoVazio titulo="Nada por aqui" {...vazio} />;

  function celula(linha: T, col: Coluna<T>) {
    if (col.render) return col.render(linha);
    const v = (linha as Record<string, unknown>)[col.chave];
    return v === null || v === undefined ? '' : String(v);
  }

  return (
    <div className={mergeClasses(e.wrap, cabecalhoFixo && e.wrapCabecalhoFixo)}>
      <table className={mergeClasses(e.tabela, compacta && e.compacta, cabecalhoFixo && e.cabecalhoFixo)} aria-label={ariaLabel}>
        <thead>
          <tr>
            {colunas.map((c) => <th key={c.chave} className={mergeClasses(e.th, c.alinhar === 'direita' && e.direita, c.alinhar === 'centro' && e.centro)} style={c.largura ? { width: c.largura } : undefined}>{c.rotulo}</th>)}
            {acoesLinha && <th className={e.th} aria-label="Ações" />}
          </tr>
        </thead>
        <tbody>
          {linhas.map((linha) => {
            const chave = chaveLinha(linha);
            const aberta = expansivel?.aberta(linha) ?? false;
            const clicavel = !!aoClicarLinha;
            return (
              <Fragment key={chave}>
                <tr
                  className={mergeClasses(e.tr, clicavel && e.trClicavel)}
                  onClick={clicavel ? () => aoClicarLinha(linha) : undefined}
                  aria-expanded={expansivel ? aberta : undefined}
                  tabIndex={clicavel ? 0 : undefined}
                  onKeyDown={clicavel ? (ev) => { if (ev.key === 'Enter' || ev.key === ' ') { ev.preventDefault(); aoClicarLinha(linha); } } : undefined}
                >
                  {colunas.map((c, i) => (
                    <td key={c.chave} className={mergeClasses(e.td, compacta && e.tdCompacta, i === 0 && e.tdPrimeira, i === colunas.length - 1 && !acoesLinha && e.tdUltima, c.alinhar === 'direita' && e.direita, c.alinhar === 'centro' && e.centro)}>
                      {celula(linha, c)}
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
