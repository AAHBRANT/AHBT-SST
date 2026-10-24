import { makeStyles, tokens } from '@fluentui/react-components';

const useStyles = makeStyles({
  governanca: {
    display: 'grid',
    gridTemplateColumns: 'repeat(4, minmax(180px, 1fr))',
    gap: '12px',
    '@media (max-width: 980px)': {
      gridTemplateColumns: 'repeat(2, minmax(180px, 1fr))',
    },
    '@media (max-width: 620px)': {
      gridTemplateColumns: '1fr',
    },
  },
  etapa: {
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: '8px',
    padding: '14px',
    background: tokens.colorNeutralBackground1,
    display: 'grid',
    gap: '8px',
    minHeight: '132px',
  },
  etapaAtiva: {
    border: '1px solid #1B6B55',
    boxShadow: 'inset 3px 0 0 #1B6B55',
  },
  etapaNumero: {
    width: '24px',
    height: '24px',
    borderRadius: '50%',
    display: 'grid',
    placeItems: 'center',
    background: '#E5F1EC',
    color: '#10483B',
    fontSize: '12px',
    fontWeight: 800,
  },
  etapaTitulo: {
    margin: 0,
    fontSize: '13px',
    fontWeight: 800,
    color: tokens.colorNeutralForeground1,
  },
  etapaTexto: {
    margin: 0,
    fontSize: '12px',
    lineHeight: '18px',
    color: tokens.colorNeutralForeground2,
  },
});

const ETAPAS = [
  {
    titulo: 'Abertura do chamado',
    texto: 'O pedido do usuário vira um registro estruturado, com módulo, severidade, descrição e contexto da tela.',
  },
  {
    titulo: 'Triagem assistida',
    texto: 'A IA responde dúvidas operacionais e mantém rastreável o diagnóstico usado para classificar o chamado.',
  },
  {
    titulo: 'Aprovação e execução',
    texto: 'Bugs, melhorias e novas funcionalidades entram como demanda técnica antes de qualquer alteração real.',
  },
  {
    titulo: 'Validação do solicitante',
    texto: 'O fechamento deve voltar ao usuário para confirmar se a orientação ou correção resolveu o problema.',
  },
];

// Esteira de 4 etapas compartilhada entre o card de triagem (chamado recém-criado) e a tela de
// detalhe (chamado existente, com status persistido) — ver esteiraSuporteIa.ts para o mapeamento
// status -> etapa ativa.
export function EsteiraSuporteIa({ etapaAtiva }: { etapaAtiva: number | null }) {
  const estilos = useStyles();
  return (
    <section className={estilos.governanca} aria-label="Esteira governada do suporte com IA">
      {ETAPAS.map((etapa, indice) => (
        <div key={etapa.titulo} className={`${estilos.etapa} ${etapaAtiva === indice ? estilos.etapaAtiva : ''}`}>
          <span className={estilos.etapaNumero}>{indice + 1}</span>
          <h3 className={estilos.etapaTitulo}>{etapa.titulo}</h3>
          <p className={estilos.etapaTexto}>{etapa.texto}</p>
        </div>
      ))}
    </section>
  );
}
