import { useState } from 'react';
import { Button, Field, Input, Text } from '@fluentui/react-components';
import { ScanObject24Regular } from '@fluentui/react-icons';
import { api, type ResolverTagDto } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { ResolverTagResultado } from './ResolverTagResultado';

// Web NFC (NDEFReader) só existe em Android Chrome/Edge sobre HTTPS. Em outros navegadores
// (iOS, desktop) não há suporte nenhum, então a leitura manual do UID é o único caminho.
const nfcSuportado = typeof window !== 'undefined' && 'NDEFReader' in window;

export function LeitorNfcTab() {
  const estilos = usePageStyles();
  const [lendo, setLendo] = useState(false);
  const [uidManual, setUidManual] = useState('');
  const [resultado, setResultado] = useState<ResolverTagDto | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  async function resolver(uid: string) {
    try {
      setErro(null);
      setResultado(null);
      const resolvido = await api.tagsIdentificacao.resolverPorUid(uid);
      setResultado(resolvido);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao resolver UID.');
    }
  }

  async function iniciarLeituraNfc() {
    if (!nfcSuportado) return;
    try {
      setErro(null);
      setLendo(true);
      // @ts-expect-error NDEFReader ainda não tem tipos oficiais no lib.dom.d.ts do TS.
      const reader = new window.NDEFReader();
      await reader.scan();
      reader.onreading = (evento: { serialNumber?: string }) => {
        setLendo(false);
        if (evento.serialNumber) resolver(evento.serialNumber);
      };
      reader.onreadingerror = () => {
        setLendo(false);
        setErro('Falha ao ler a tag NFC. Aproxime o dispositivo novamente.');
      };
    } catch (e) {
      setLendo(false);
      setErro(e instanceof Error ? e.message : 'Falha ao iniciar leitura NFC.');
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Leitor / Teste NFC</Text>
      </div>

      {nfcSuportado ? (
        <>
          <Text as="p">
            Este dispositivo suporta leitura NFC pelo navegador. Toque no botão abaixo e aproxime a tag.
          </Text>
          <div className={estilos.formActions}>
            <Button appearance="primary" icon={<ScanObject24Regular />} onClick={iniciarLeituraNfc} disabled={lendo}>
              {lendo ? 'Aproxime a tag...' : 'Iniciar leitura NFC'}
            </Button>
          </div>
        </>
      ) : (
        <Text as="p">
          Este navegador/dispositivo não suporta leitura NFC via Web NFC (suporte hoje limitado a Android
          Chrome/Edge). Digite o UID manualmente ou use um leitor de QR Code.
        </Text>
      )}

      <div className={estilos.sectionTitle}>Leitura Manual</div>
      <div className={estilos.formGrid}>
        <div className={estilos.col4}>
          <Field label="UID manual (ou lido via QR Code)">
            <Input value={uidManual} onChange={(_, d) => setUidManual(d.value)} />
          </Field>
        </div>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="secondary" onClick={() => resolver(uidManual)} disabled={!uidManual}>
          Resolver
        </Button>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}
      {resultado && <ResolverTagResultado resultado={resultado} />}
    </div>
  );
}
