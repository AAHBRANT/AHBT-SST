import { useId, useState, type KeyboardEvent } from 'react';
import { Legenda, designTokens, tokensUi } from '@ui';
import { Dismiss12Regular } from '@fluentui/react-icons';
import { mapaCorporal as mapa, ordenarRegioesCorpo, rotuloRegiaoCorpo } from './regioesCorpo';

// Mapa corporal da lesão (registro de ocorrência). O desenho vem de mapaCorporal.json — gerado por
// scripts/mapa-corporal/gerar_mapa_corporal.py e embutido também na API, que desenha o mesmo mapa no
// relatório PDF (MapaCorporalSvg.cs).

const vistas = mapa.vistas;

interface Props {
  selecionadas: string[];
  /** Sem esta prop o mapa é só leitura (detalhe da ocorrência). */
  aoAlterar?: (codigos: string[]) => void;
  /** Altura de cada figura, em px. */
  altura?: number;
}

const corpoClaro = '#f1f3f6';
const corpoMedio = '#dfe4ea';
const corpoEscuro = '#b9c3cf';
const contorno = '#5b6b80';
const tracoMuscular = '#8796ab';

export function MapaCorporal({ selecionadas, aoAlterar, altura = 420 }: Props) {
  const id = useId().replace(/:/g, '');
  const [emFoco, setEmFoco] = useState<string | null>(null);
  const editavel = Boolean(aoAlterar);
  const marcadas = new Set(selecionadas);

  function alternar(codigo: string) {
    if (!aoAlterar) return;
    aoAlterar(ordenarRegioesCorpo(marcadas.has(codigo) ? selecionadas.filter((c) => c !== codigo) : [...selecionadas, codigo]));
  }

  function teclado(evento: KeyboardEvent<SVGPathElement>, codigo: string) {
    if (evento.key === 'Enter' || evento.key === ' ') {
      evento.preventDefault();
      alternar(codigo);
    }
  }

  return (
    <div style={{ display: 'grid', gap: tokensUi.espaco.md }}>
      {editavel && <Legenda>Clique nas partes do corpo atingidas (frente e costas). Clique de novo para desmarcar.</Legenda>}
      <div style={{ display: 'flex', flexWrap: 'wrap', justifyContent: 'center', gap: tokensUi.espaco.xl }}>
        {vistas.map((vista) => (
          <figure key={vista.id} style={{ margin: 0, display: 'grid', justifyItems: 'center', gap: tokensUi.espaco.xs }}>
            <figcaption style={{ fontSize: 11, fontWeight: 700, letterSpacing: '0.06em', color: designTokens.colorNeutralMedium }}>
              {vista.titulo.toUpperCase()}
            </figcaption>
            <svg
              viewBox={mapa.viewBox}
              height={altura}
              width={(altura * 120) / 440}
              role="group"
              aria-label={vista.titulo}
              style={{ overflow: 'visible' }}
            >
              <defs>
                <clipPath id={`silhueta-${id}-${vista.id}`}>
                  <path d={mapa.silhueta} />
                </clipPath>
                <linearGradient id={`volume-${id}-${vista.id}`} x1="40" x2="160" gradientUnits="userSpaceOnUse">
                  <stop offset="0" stopColor={corpoEscuro} />
                  <stop offset="0.22" stopColor={corpoMedio} />
                  <stop offset="0.5" stopColor={corpoClaro} />
                  <stop offset="0.78" stopColor={corpoMedio} />
                  <stop offset="1" stopColor={corpoEscuro} />
                </linearGradient>
                <filter id={`brilho-${id}`} x="-20%" y="-20%" width="140%" height="140%">
                  <feGaussianBlur stdDeviation="1.6" result="borrado" />
                  <feMerge>
                    <feMergeNode in="borrado" />
                    <feMergeNode in="SourceGraphic" />
                  </feMerge>
                </filter>
              </defs>
              <path d={mapa.silhueta} fill={`url(#volume-${id}-${vista.id})`} />
              <g clipPath={`url(#silhueta-${id}-${vista.id})`}>
                {vista.regioes.map((regiao) => {
                  const marcada = marcadas.has(regiao.codigo);
                  const destacada = emFoco === regiao.codigo;
                  return (
                    <path
                      key={regiao.codigo}
                      d={regiao.d}
                      fill={marcada ? tokensUi.status.alerta.tinta : destacada ? tokensUi.status.alerta.tinta : 'transparent'}
                      fillOpacity={marcada ? 0.82 : destacada ? 0.22 : 1}
                      stroke={destacada ? tokensUi.status.alerta.tinta : 'none'}
                      strokeWidth={0.6}
                      strokeDasharray={destacada && !marcada ? '1.5 1' : undefined}
                      filter={marcada ? `url(#brilho-${id})` : undefined}
                      style={{ cursor: editavel ? 'pointer' : 'default', outline: 'none', transition: 'fill-opacity 120ms' }}
                      role={editavel ? 'checkbox' : undefined}
                      aria-checked={editavel ? marcada : undefined}
                      aria-label={editavel ? rotuloRegiaoCorpo(regiao.codigo) : undefined}
                      tabIndex={editavel ? 0 : undefined}
                      onClick={() => alternar(regiao.codigo)}
                      onKeyDown={(e) => teclado(e, regiao.codigo)}
                      onMouseEnter={() => editavel && setEmFoco(regiao.codigo)}
                      onMouseLeave={() => setEmFoco((atual) => (atual === regiao.codigo ? null : atual))}
                      onFocus={() => setEmFoco(regiao.codigo)}
                      onBlur={() => setEmFoco((atual) => (atual === regiao.codigo ? null : atual))}
                    >
                      <title>{rotuloRegiaoCorpo(regiao.codigo)}</title>
                    </path>
                  );
                })}
              </g>
              <g fill="none" stroke={tracoMuscular} strokeWidth={0.55} strokeLinecap="round" opacity={0.8} pointerEvents="none">
                {vista.detalhes.map((d, i) => (
                  <path key={i} d={d} />
                ))}
              </g>
              <path d={mapa.silhueta} fill="none" stroke={contorno} strokeWidth={0.8} strokeLinejoin="round" pointerEvents="none" />
              {/* Lado do corpo: na frente, o lado direito do trabalhador fica à esquerda de quem olha. */}
              <g fontSize={7} fontWeight={700} fill={designTokens.colorNeutralMedium} textAnchor="middle" pointerEvents="none">
                <text x={46} y={437}>{vista.id === 'F' ? 'D' : 'E'}</text>
                <text x={154} y={437}>{vista.id === 'F' ? 'E' : 'D'}</text>
              </g>
            </svg>
          </figure>
        ))}
      </div>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: tokensUi.espaco.xs, justifyContent: 'center' }} aria-live="polite">
        {selecionadas.length === 0 ? (
          <Legenda>Nenhuma parte do corpo marcada.</Legenda>
        ) : (
          ordenarRegioesCorpo(selecionadas).map((codigo) => (
            <span
              key={codigo}
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: 4,
                padding: '2px 8px',
                borderRadius: tokensUi.raio.full,
                fontSize: 12,
                fontWeight: 600,
                color: tokensUi.status.alerta.tinta,
                backgroundColor: tokensUi.status.alerta.fundo,
              }}
            >
              {rotuloRegiaoCorpo(codigo)}
              {editavel && (
                <button
                  type="button"
                  aria-label={`Desmarcar ${rotuloRegiaoCorpo(codigo)}`}
                  onClick={() => alternar(codigo)}
                  style={{ display: 'inline-flex', border: 'none', background: 'none', padding: 0, cursor: 'pointer', color: 'inherit' }}
                >
                  <Dismiss12Regular />
                </button>
              )}
            </span>
          ))
        )}
      </div>
    </div>
  );
}
