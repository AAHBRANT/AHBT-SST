import { Text } from '@fluentui/react-components';
import { StatusChip } from '@ui';
import { formatarDataFoto, pendenciasFoto, type DadosFoto } from '../../lib/dadosFoto';

export function ResumoDadosFoto({ dados }: { dados?: DadosFoto | null }) {
  const legado = !dados;
  const pendencias = pendenciasFoto(dados);
  return <div style={{ display: 'flex', flexDirection: 'column', gap: 4, marginTop: 8, overflowWrap: 'anywhere' }}>
    <Text size={200}>{dados?.origem === 'arquivo' ? 'Anexada da galeria — sem data/hora e sem geolocalização comprovadas' : formatarDataFoto(dados)}</Text>
    <Text size={200}>Obra: {dados?.obraNome || 'não registrada'}</Text>
    {dados?.local && <Text size={200}>Local: {dados.local}</Text>}
    {dados?.latitude != null && dados.longitude != null && <Text size={200}>
      {dados.latitude.toFixed(6)}, {dados.longitude.toFixed(6)}{dados.precisaoMetros != null ? ` · precisão ${Math.round(dados.precisaoMetros)} m` : ' · precisão não informada'}
    </Text>}
    <StatusChip tom={legado ? 'neutro' : pendencias.length ? 'atencao' : 'ok'}>
      {legado ? 'Foto legada sem metadados' : pendencias.length ? 'Foto com pendência' : 'Dados da foto completos'}
    </StatusChip>
    {pendencias.length > 0 && <Text size={200}>Falta: {pendencias.join(', ')}.</Text>}
  </div>;
}
