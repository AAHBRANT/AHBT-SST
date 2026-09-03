import { Text } from '@fluentui/react-components';
import { Wrench24Regular } from '@fluentui/react-icons';
import { usePageStyles } from './pageStyles';
import { designTokens } from '../theme';

interface EmConstrucaoPageProps {
  titulo: string;
  descricao: string;
  mostrarTitulo?: boolean;
}

// Placeholder para itens que já ganharam lugar fixo na navegação (reorganização de sidebar,
// 2026-08-31) mas ainda não têm tela/dado próprio no sistema — evita esconder a decisão de
// onde cada coisa vai ficar, sem fingir que a funcionalidade já existe.
export function EmConstrucaoPage({ titulo, descricao, mostrarTitulo = true }: EmConstrucaoPageProps) {
  const estilos = usePageStyles();

  return (
    <div>
      {mostrarTitulo && (
        <div style={{ marginBottom: 16 }}>
          <Text size={500} weight="semibold">
            {titulo}
          </Text>
        </div>
      )}
      <div
        className={estilos.card}
        style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12, padding: '48px 24px' }}
      >
        <Wrench24Regular fontSize={32} style={{ color: designTokens.colorNeutralMedium }} />
        <Text weight="semibold">Em construção</Text>
        <Text style={{ color: designTokens.colorNeutralMedium, textAlign: 'center', maxWidth: 420 }}>
          {descricao}
        </Text>
      </div>
    </div>
  );
}
