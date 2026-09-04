import { Text } from '@fluentui/react-components';
import { designTokens } from '../theme';

interface EstadoVazioProps {
  mensagem: string;
}

// Diferencia "carregando" de "não há nada" nas telas de listagem (pedido do usuário, 31/08) — antes
// disso, uma tabela vazia enquanto os dados ainda chegavam da API parecia idêntica a uma tabela
// vazia porque o cadastro está mesmo vazio. Usar dentro de uma linha/célula de tabela, ou sozinho
// abaixo de uma lista não-tabular.
export function EstadoVazio({ mensagem }: EstadoVazioProps) {
  return (
    <div style={{ padding: '32px 16px', textAlign: 'center' }}>
      <Text style={{ color: designTokens.colorNeutralMedium }}>{mensagem}</Text>
    </div>
  );
}
