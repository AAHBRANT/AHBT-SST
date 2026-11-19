import { Text, makeStyles } from '@fluentui/react-components';
import { Attach16Regular } from '@fluentui/react-icons';
import { StatusChip } from '@ui';
import { formatarDataFoto, pendenciasFoto, type DadosFoto } from '../../lib/dadosFoto';

// Em colunas estreitas (2 fotos lado a lado no celular) a etiqueta precisa quebrar linha; sem isso ela
// passava por baixo da foto vizinha (conferido 19/11/2026).
const useEstilos = makeStyles({ etiqueta: { whiteSpace: 'normal', maxWidth: '100%', alignSelf: 'flex-start' } });

// Foto de arquivo (Ocorrências, 18/11/2026): mostra o que veio gravado na própria foto, sempre como
// "lido da foto" — nada disso é comprovado pelo sistema, diferente da captura pela câmera.
function ResumoFotoArquivo({ dados }: { dados: DadosFoto }) {
  const estilos = useEstilos();
  const temData = !!dados.capturadaEm && Number.isFinite(Date.parse(dados.capturadaEm));
  const temGps = dados.latitude != null && dados.longitude != null;
  return <div style={{ display: 'flex', flexDirection: 'column', gap: 4, marginTop: 8, overflowWrap: 'anywhere' }}>
    <StatusChip className={estilos.etiqueta} tom="info" icone={<Attach16Regular />}>Enviada de arquivo</StatusChip>
    {temData
      ? <Text size={200}>Data da foto: {formatarDataFoto(dados)} (lida da foto)</Text>
      : <Text size={200}>Data e local não comprovados pela foto</Text>}
    <Text size={200}>Obra: {dados.obraNome || 'não registrada'}</Text>
    {dados.local && <Text size={200}>Local: {dados.local}</Text>}
    {temGps && <Text size={200}>{dados.latitude!.toFixed(6)}, {dados.longitude!.toFixed(6)} (GPS gravado na foto)</Text>}
  </div>;
}

export function ResumoDadosFoto({ dados }: { dados?: DadosFoto | null }) {
  const estilos = useEstilos();
  if (dados?.origem === 'arquivo') return <ResumoFotoArquivo dados={dados} />;
  const legado = !dados;
  const pendencias = pendenciasFoto(dados);
  return <div style={{ display: 'flex', flexDirection: 'column', gap: 4, marginTop: 8, overflowWrap: 'anywhere' }}>
    <Text size={200}>{formatarDataFoto(dados)}</Text>
    <Text size={200}>Obra: {dados?.obraNome || 'não registrada'}</Text>
    {dados?.local && <Text size={200}>Local: {dados.local}</Text>}
    {dados?.latitude != null && dados.longitude != null && <Text size={200}>
      {dados.latitude.toFixed(6)}, {dados.longitude.toFixed(6)}{dados.precisaoMetros != null ? ` · precisão ${Math.round(dados.precisaoMetros)} m` : ' · precisão não informada'}
    </Text>}
    <StatusChip className={estilos.etiqueta} tom={legado ? 'neutro' : pendencias.length ? 'atencao' : 'ok'}>
      {legado ? 'Foto legada sem metadados' : pendencias.length ? 'Foto com pendência' : 'Dados da foto completos'}
    </StatusChip>
    {pendencias.length > 0 && <Text size={200}>Falta: {pendencias.join(', ')}.</Text>}
  </div>;
}
