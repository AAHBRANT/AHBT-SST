import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Badge,
  Button,
  Field,
  Input,
  Tab,
  TabList,
  Text,
  Textarea,
  type SelectTabData,
  type SelectTabEvent,
} from '@fluentui/react-components';
import { ArrowLeft24Regular, Checkmark24Regular, Dismiss24Regular } from '@fluentui/react-icons';
import { api, StatusApr, statusAprLabel, type AprDetalhe } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { AprEtapasTab } from './AprEtapasTab';
import { AprAssinaturasTab } from './AprAssinaturasTab';

type AbaApr = 'etapas' | 'assinaturas';

export function AprDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const estilos = usePageStyles();
  const [aba, setAba] = useState<AbaApr>('etapas');
  const [detalhe, setDetalhe] = useState<AprDetalhe | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [aprovadoPorUsuarioId, setAprovadoPorUsuarioId] = useState('');
  const [motivoReprovacao, setMotivoReprovacao] = useState('');
  const [processando, setProcessando] = useState(false);

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      setDetalhe(await api.aprs.obterDetalhe(id));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar APR.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  async function aprovar() {
    if (!id || !aprovadoPorUsuarioId) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.aprs.aprovar(id, aprovadoPorUsuarioId);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao aprovar APR.');
    } finally {
      setProcessando(false);
    }
  }

  async function reprovar() {
    if (!id || !motivoReprovacao) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.aprs.reprovar(id, motivoReprovacao);
      setMotivoReprovacao('');
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao reprovar APR.');
    } finally {
      setProcessando(false);
    }
  }

  if (!id) {
    return <Text>APR não encontrada.</Text>;
  }

  const podeDecidir = detalhe?.apr.status === StatusApr.AguardandoAprovacao || detalhe?.apr.status === StatusApr.EmElaboracao;

  return (
    <div>
      <Button
        appearance="subtle"
        icon={<ArrowLeft24Regular />}
        onClick={() => navigate('/operacao/apr')}
        style={{ marginBottom: 12 }}
      >
        Voltar para APR
      </Button>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        {detalhe ? (
          <>
            <Text size={500} weight="semibold">
              {detalhe.apr.atividadeNome}
            </Text>
            <div style={{ display: 'flex', gap: 16, marginTop: 8, flexWrap: 'wrap', alignItems: 'center' }}>
              <Text>Local: {detalhe.apr.local}</Text>
              <Text>Data: {detalhe.apr.data?.slice(0, 10)}</Text>
              {detalhe.apr.validade && <Text>Validade: {detalhe.apr.validade.slice(0, 10)}</Text>}
              <Badge appearance="tint">{statusAprLabel[detalhe.apr.status]}</Badge>
            </div>
            {detalhe.apr.status === StatusApr.Aprovada && (
              <Text style={{ marginTop: 8 }}>
                Aprovada por {detalhe.apr.aprovadoPorUsuarioNome ?? detalhe.apr.aprovadoPorUsuarioId} em{' '}
                {detalhe.apr.dataAprovacao?.slice(0, 10)}
              </Text>
            )}
            {detalhe.apr.status === StatusApr.Reprovada && detalhe.apr.motivoReprovacao && (
              <Text style={{ marginTop: 8 }}>Motivo da reprovação: {detalhe.apr.motivoReprovacao}</Text>
            )}
            {detalhe.responsaveis.length > 0 && (
              <Text style={{ marginTop: 8 }}>
                Responsáveis: {detalhe.responsaveis.map((r) => r.trabalhadorNome).join(', ')}
              </Text>
            )}

            {podeDecidir && (
              <div style={{ display: 'flex', gap: 24, marginTop: 16, flexWrap: 'wrap', alignItems: 'flex-end' }}>
                <Field label="ID do usuário aprovador (GUID)">
                  <Input value={aprovadoPorUsuarioId} onChange={(_, d) => setAprovadoPorUsuarioId(d.value)} />
                </Field>
                <Button
                  appearance="primary"
                  icon={<Checkmark24Regular />}
                  onClick={aprovar}
                  disabled={processando || !aprovadoPorUsuarioId}
                >
                  Aprovar
                </Button>
                <Field label="Motivo da reprovação">
                  <Textarea value={motivoReprovacao} onChange={(_, d) => setMotivoReprovacao(d.value)} />
                </Field>
                <Button
                  appearance="secondary"
                  icon={<Dismiss24Regular />}
                  onClick={reprovar}
                  disabled={processando || !motivoReprovacao}
                >
                  Reprovar
                </Button>
              </div>
            )}
          </>
        ) : (
          <Text>Carregando...</Text>
        )}
      </div>

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaApr)}
        style={{ marginBottom: 16 }}
      >
        <Tab value="etapas">Etapas</Tab>
        <Tab value="assinaturas">Assinaturas</Tab>
      </TabList>

      {aba === 'etapas' && <AprEtapasTab aprId={id} />}
      {aba === 'assinaturas' && <AprAssinaturasTab aprId={id} />}
    </div>
  );
}
