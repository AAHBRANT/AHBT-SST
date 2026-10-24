import { useState, type ReactNode } from 'react';
import { Button, makeStyles } from '@fluentui/react-components';
import { ChevronDown16Regular, ChevronUp16Regular } from '@fluentui/react-icons';
import { designTokens, tokensUi } from '../../tokens/tokens';
import { useTipografia } from '../../tokens/tipografia';

const CHAVE_STORAGE = 'sst.dashboardTopo.recolhido';
const LARGURA_CELULAR = 640;

const useStyles = makeStyles({
  root: {
    border: `1px solid ${designTokens.colorCardBorder}`,
    borderRadius: tokensUi.raio.lg,
    padding: tokensUi.espaco.lg,
    marginBottom: tokensUi.espaco.lg,
    backgroundColor: designTokens.colorSurface,
  },
  topo: { display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: tokensUi.espaco.md },
  rotulo: { color: designTokens.colorNeutralMedium, textTransform: 'uppercase', letterSpacing: '0.06em' },
  corpo: { marginTop: tokensUi.espaco.lg },
});

// Estado inicial: a escolha salva do usuário; sem escolha, abre recolhido em tela estreita (Teams
// mobile) para o dashboard não empurrar a lista para baixo. Storage pode falhar (janela privada).
function recolhidoInicial(): boolean {
  try {
    const salvo = window.localStorage.getItem(CHAVE_STORAGE);
    if (salvo !== null) return salvo === '1';
  } catch {
    /* sem storage: cai no padrão por largura */
  }
  return window.innerWidth < LARGURA_CELULAR;
}

// Faixa de dashboard no topo da página do módulo (substitui a antiga aba "Dashboard"). O conteúdo
// continua montado quando recolhido (só fica oculto), para não refazer as consultas ao expandir.
export function DashboardTopo({ children, titulo = 'Dashboard' }: { children: ReactNode; titulo?: string }) {
  const estilos = useStyles();
  const tipo = useTipografia();
  const [recolhido, setRecolhido] = useState(recolhidoInicial);

  function alternar() {
    setRecolhido((atual) => {
      const novo = !atual;
      try {
        window.localStorage.setItem(CHAVE_STORAGE, novo ? '1' : '0');
      } catch {
        /* preferência só vale nesta sessão */
      }
      return novo;
    });
  }

  return (
    <section className={estilos.root} aria-label={titulo}>
      <div className={estilos.topo}>
        <span className={`${tipo.corpo} ${estilos.rotulo}`}>{titulo}</span>
        <Button
          appearance="subtle"
          size="small"
          icon={recolhido ? <ChevronDown16Regular /> : <ChevronUp16Regular />}
          aria-expanded={!recolhido}
          onClick={alternar}
        >
          {recolhido ? 'Expandir' : 'Recolher'}
        </Button>
      </div>
      <div className={estilos.corpo} hidden={recolhido}>
        {children}
      </div>
    </section>
  );
}
