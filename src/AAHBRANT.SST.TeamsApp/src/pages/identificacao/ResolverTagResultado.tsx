import { Text } from '@fluentui/react-components';
import { tipoTagLabel, statusTagLabel, tipoEntidadeVinculadaLabel, type ResolverTagDto } from '../../lib/api';

export function ResolverTagResultado({ resultado }: { resultado: ResolverTagDto }) {
  return (
    <Text as="p">
      Tag {resultado.uid} ({tipoTagLabel[resultado.tipo]}) — status {statusTagLabel[resultado.status]}.{' '}
      {resultado.entidadeVinculadaTipo
        ? `Vinculada a ${tipoEntidadeVinculadaLabel[resultado.entidadeVinculadaTipo]}: ${
            resultado.entidadeVinculadaNome ?? resultado.entidadeVinculadaId
          }`
        : 'Sem vínculo.'}
    </Text>
  );
}
