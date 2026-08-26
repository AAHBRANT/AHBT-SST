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
import {
  ArrowLeft24Regular,
  CheckmarkCircle24Regular,
  LockClosed24Regular,
  Signature24Regular,
} from '@fluentui/react-icons';
import { api, StatusPt, statusPtLabel, type PermissaoTrabalhoDetalhe } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { PermissaoTrabalhoControlesTab } from './PermissaoTrabalhoControlesTab';
import { PermissaoTrabalhoRequisitosTab } from './PermissaoTrabalhoRequisitosTab';

type AbaPt = 'controles' | 'requisitos';

export function PermissaoTrabalhoDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const estilos = usePageStyles();
  const [aba, setAba] = useState<AbaPt>('requisitos');
  const [detalhe, setDetalhe] = useState<PermissaoTrabalhoDetalhe | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [autorizadoPorUsuarioId, setAutorizadoPorUsuarioId] = useState('');
  const [encerradaPorUsuarioId, setEncerradaPorUsuarioId] = useState('');
  const [observacoesEncerramento, setObservacoesEncerramento] = useState('');
  const [processando, setProcessando] = useState(false);

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      setDetalhe(await api.permissoesTrabalho.obterDetalhe(id));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar Permissão de Trabalho.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  async function autorizar() {
    if (!id || !autorizadoPorUsuarioId) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.permissoesTrabalho.autorizar(id, autorizadoPorUsuarioId);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao autorizar PT.');
    } finally {
      setProcessando(false);
    }
  }

  async function encerrar() {
    if (!id || !encerradaPorUsuarioId) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.permissoesTrabalho.encerrar(id, encerradaPorUsuarioId, observacoesEncerramento || null);
      setObservacoesEncerramento('');
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao encerrar PT.');
    } finally {
      setProcessando(false);
    }
  }

  if (!id) {
    return <Text>Permissão de Trabalho não encontrada.</Text>;
  }

  const pt = detalhe?.permissaoTrabalho;

  return (
    <div>
      <Button
        appearance="subtle"
        icon={<ArrowLeft24Regular />}
        onClick={() => navigate('/operacao/pt')}
        style={{ marginBottom: 12 }}
      >
        Voltar para PT
      </Button>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        {pt ? (
          <>
            <Text size={500} weight="semibold">
              {pt.atividadeNome}
            </Text>
            <div style={{ display: 'flex', gap: 16, marginTop: 8, flexWrap: 'wrap', alignItems: 'center' }}>
              <Text>Local: {pt.local}</Text>
              <Text>Data: {pt.data?.slice(0, 10)}</Text>
              {pt.validade && <Text>Validade: {pt.validade.slice(0, 10)}</Text>}
              <Badge appearance="tint">{statusPtLabel[pt.status]}</Badge>
            </div>
            {pt.status === StatusPt.Autorizada && (
              <Text style={{ marginTop: 8 }}>
                Autorizada por {pt.autorizadoPorUsuarioNome ?? pt.autorizadoPorUsuarioId} em{' '}
                {pt.dataAutorizacao?.slice(0, 10)}
              </Text>
            )}
            {pt.status === StatusPt.Encerrada && (
              <Text style={{ marginTop: 8 }}>
                Encerrada por {pt.encerradaPorUsuarioNome ?? pt.encerradaPorUsuarioId} em{' '}
                {pt.dataEncerramento?.slice(0, 10)}
                {pt.observacoesEncerramento && ` — ${pt.observacoesEncerramento}`}
              </Text>
            )}
            {detalhe && detalhe.perigos.length > 0 && (
              <Text style={{ marginTop: 8 }}>Perigos: {detalhe.perigos.map((p) => p.perigoNome).join(', ')}</Text>
            )}
            {detalhe && detalhe.responsaveis.length > 0 && (
              <Text style={{ marginTop: 8 }}>
                Responsáveis: {detalhe.responsaveis.map((r) => r.trabalhadorNome).join(', ')}
              </Text>
            )}

            <div style={{ marginTop: 16 }}>
              <Button icon={<Signature24Regular />} onClick={() => navigate(`/operacao/pt/${id}/assinar`)}>
                Assinar PT
              </Button>
            </div>

            {pt.status === StatusPt.EmElaboracao && (
              <div style={{ display: 'flex', gap: 16, marginTop: 16, flexWrap: 'wrap', alignItems: 'flex-end' }}>
                <Field label="ID do usuário autorizador (GUID)">
                  <Input value={autorizadoPorUsuarioId} onChange={(_, d) => setAutorizadoPorUsuarioId(d.value)} />
                </Field>
                <Button
                  appearance="primary"
                  icon={<CheckmarkCircle24Regular />}
                  onClick={autorizar}
                  disabled={processando || !autorizadoPorUsuarioId}
                >
                  Autorizar
                </Button>
              </div>
            )}

            {pt.status === StatusPt.Autorizada && (
              <div style={{ display: 'flex', gap: 16, marginTop: 16, flexWrap: 'wrap', alignItems: 'flex-end' }}>
                <Field label="ID do usuário que encerra (GUID)">
                  <Input value={encerradaPorUsuarioId} onChange={(_, d) => setEncerradaPorUsuarioId(d.value)} />
                </Field>
                <Field label="Observações">
                  <Textarea value={observacoesEncerramento} onChange={(_, d) => setObservacoesEncerramento(d.value)} />
                </Field>
                <Button
                  appearance="secondary"
                  icon={<LockClosed24Regular />}
                  onClick={encerrar}
                  disabled={processando || !encerradaPorUsuarioId}
                >
                  Encerrar
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
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaPt)}
        style={{ marginBottom: 16 }}
      >
        <Tab value="requisitos">Requisitos</Tab>
        <Tab value="controles">Controles</Tab>
      </TabList>

      {aba === 'requisitos' && <PermissaoTrabalhoRequisitosTab permissaoTrabalhoId={id} />}
      {aba === 'controles' && <PermissaoTrabalhoControlesTab permissaoTrabalhoId={id} />}
    </div>
  );
}
