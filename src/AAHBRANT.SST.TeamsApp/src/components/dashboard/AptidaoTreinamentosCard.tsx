import { useState } from 'react';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import type { AptidaoCurso } from '../../lib/api';
import { Card, Legenda, designTokens, tokensUi, usePaletaGraficos } from '@ui';

interface AptidaoTreinamentosCardProps {
  cursos: AptidaoCurso[];
  escopo: string;
}

const useStyles = makeStyles({
  seletor: { display: 'flex', flexWrap: 'wrap', gap: '8px', marginBottom: '16px' },
  botao: {
    border: `1px solid ${designTokens.colorCardBorder}`,
    backgroundColor: designTokens.colorSurface,
    color: designTokens.colorNeutralMedium,
    borderRadius: '99px',
    padding: '5px 12px',
    fontSize: '12.5px',
    fontWeight: 600,
    cursor: 'pointer',
    ':hover': { border: `1px solid ${designTokens.colorNeutralMedium}` },
  },
  botaoAtivo: {
    backgroundColor: designTokens.colorPrimary,
    border: `1px solid ${designTokens.colorPrimary}`,
    color: '#ffffff',
    ':hover': { border: `1px solid ${designTokens.colorPrimary}` },
  },
  destaque: { display: 'flex', alignItems: 'baseline', flexWrap: 'wrap', gap: '8px', marginBottom: '12px' },
  numero: {
    fontSize: '40px',
    lineHeight: '40px',
    fontWeight: 800,
    letterSpacing: '-0.02em',
    fontVariantNumeric: 'tabular-nums',
  },
  legendaNumero: { fontSize: '13px', fontWeight: 600, color: designTokens.colorNeutralMedium },
  barra: {
    display: 'flex',
    height: '10px',
    borderRadius: '99px',
    overflow: 'hidden',
    gap: '2px',
    backgroundColor: designTokens.colorCardBorder,
  },
  contadores: {
    display: 'grid',
    gridTemplateColumns: 'repeat(4, minmax(0, 1fr))',
    gap: '8px',
    marginTop: '14px',
    '@media (max-width: 520px)': { gridTemplateColumns: 'repeat(2, minmax(0, 1fr))' },
  },
  contador: {
    padding: '8px 10px',
    borderRadius: tokensUi.raio.md,
    border: `1px solid ${designTokens.colorCardBorder}`,
    backgroundColor: designTokens.colorNeutralLight,
    fontSize: '12px',
    color: designTokens.colorNeutralMedium,
    fontWeight: 600,
  },
  contadorValor: {
    display: 'block',
    fontSize: '20px',
    fontWeight: 800,
    color: designTokens.colorNeutralDark,
    fontVariantNumeric: 'tabular-nums',
  },
  ponto: { display: 'inline-block', width: '8px', height: '8px', borderRadius: '2px', marginRight: '6px' },
});

// "Apto" = treinamento do curso válido hoje (decisão do usuário, 04/10): ASO e EPI ficam nos cards
// deles. Os que vencem em até 30 dias ainda são aptos, mas ganham faixa própria para dar tempo de agir.
export function AptidaoTreinamentosCard({ cursos, escopo }: AptidaoTreinamentosCardProps) {
  const estilos = useStyles();
  const paleta = usePaletaGraficos();
  const [selecionadoId, setSelecionadoId] = useState<string | null>(null);

  const curso = cursos.find((c) => c.cursoId === selecionadoId) ?? cursos[0];
  const corDaFaixa = (pct: number) => (pct >= 85 ? paleta.ok : pct >= 60 ? paleta.atencao : paleta.alerta);

  const rotuloCurto = (c: AptidaoCurso) => c.nome.replace(/\s*\([^)]*\)\s*$/, '');

  return (
    <Card
      titulo="Aptidão por treinamento"
      subtitulo={`Trabalhadores que precisam do curso para a sua função, ${escopo}`}
    >
      {!curso ? (
        <Legenda>Nenhuma função tem treinamento obrigatório cadastrado na Matriz de Treinamento.</Legenda>
      ) : (
        <>
          <div className={estilos.seletor} role="group" aria-label="Escolher o treinamento">
            {cursos.map((c) => (
              <button
                key={c.cursoId}
                type="button"
                aria-pressed={c.cursoId === curso.cursoId}
                title={c.nome}
                className={mergeClasses(estilos.botao, c.cursoId === curso.cursoId && estilos.botaoAtivo)}
                onClick={(e) => {
                  e.stopPropagation();
                  setSelecionadoId(c.cursoId);
                }}
              >
                {c.normaReferencia ?? rotuloCurto(c)}
              </button>
            ))}
          </div>
          <Detalhe curso={curso} corDaFaixa={corDaFaixa} corOk={paleta.ok} corAviso={paleta.atencao} corAlerta={paleta.alerta} corNeutra={paleta.neutro} />
        </>
      )}
    </Card>
  );
}

interface DetalheProps {
  curso: AptidaoCurso;
  corDaFaixa: (pct: number) => string;
  corOk: string;
  corAviso: string;
  corAlerta: string;
  corNeutra: string;
}

function Detalhe({ curso, corDaFaixa, corOk, corAviso, corAlerta, corNeutra }: DetalheProps) {
  const estilos = useStyles();
  const aptos = curso.emDia + curso.vencemEm30Dias;
  const pct = curso.exigidos > 0 ? Math.round((aptos / curso.exigidos) * 100) : 0;
  const faixas = [
    { rotulo: 'Em dia', valor: curso.emDia, cor: corOk },
    { rotulo: 'Vencem em 30 dias', valor: curso.vencemEm30Dias, cor: corAviso },
    { rotulo: 'Vencidos', valor: curso.vencidos, cor: corAlerta },
    { rotulo: 'Sem o curso', valor: curso.semCurso, cor: corNeutra },
  ];

  return (
    <>
      <div className={estilos.destaque}>
        <span className={estilos.numero} style={{ color: corDaFaixa(pct) }}>
          {aptos}
        </span>
        <span className={estilos.legendaNumero}>
          aptos de {curso.exigidos} ({pct}%) · {curso.nome}
        </span>
      </div>
      <div
        className={estilos.barra}
        role="img"
        aria-label={`${curso.nome}: ${aptos} aptos de ${curso.exigidos}. ${curso.vencidos} vencidos e ${curso.semCurso} sem o curso.`}
      >
        {faixas.map((faixa) => (
          <span
            key={faixa.rotulo}
            style={{ width: `${(faixa.valor / curso.exigidos) * 100}%`, backgroundColor: faixa.cor }}
          />
        ))}
      </div>
      <div className={estilos.contadores}>
        {faixas.map((faixa) => (
          <div key={faixa.rotulo} className={estilos.contador}>
            <span className={estilos.contadorValor}>{faixa.valor}</span>
            <span className={estilos.ponto} style={{ backgroundColor: faixa.cor }} />
            {faixa.rotulo}
          </div>
        ))}
      </div>
    </>
  );
}
