import { useEffect, useState } from 'react';
import {
  Button,
  Checkbox,
  Field,
  Input,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
  Textarea,
} from '@fluentui/react-components';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, type AprEtapa, type NovaAprEtapa, type Perigo, type Risco } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

function etapaVazia(aprId: string, proximaOrdem: number): NovaAprEtapa {
  return { aprId, ordem: proximaOrdem, descricao: '', medidasPreventivas: '', riscosIds: [] };
}

export function AprEtapasTab({ aprId }: { aprId: string }) {
  const estilos = usePageStyles();
  const [etapas, setEtapas] = useState<AprEtapa[]>([]);
  const [riscos, setRiscos] = useState<Risco[]>([]);
  const [perigos, setPerigos] = useState<Perigo[]>([]);
  const [novaEtapa, setNovaEtapa] = useState<NovaAprEtapa>(() => etapaVazia(aprId, 1));
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [etps, rscs, prgs] = await Promise.all([
        api.aprEtapas.listar(aprId),
        api.riscos.listar(),
        api.perigos.listar(),
      ]);
      setEtapas(etps);
      setRiscos(rscs);
      setPerigos(prgs);
      setNovaEtapa(etapaVazia(aprId, etps.length + 1));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar etapas.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [aprId]);

  function rotuloRisco(riscoId: string) {
    const risco = riscos.find((r) => r.id === riscoId);
    if (!risco) return riscoId;
    const perigo = perigos.find((p) => p.id === risco.perigoId)?.nome ?? '';
    return perigo ? `${perigo} (${risco.ambiente ?? 's/ ambiente'})` : risco.ambiente ?? riscoId;
  }

  function alternarRisco(id: string, marcado: boolean) {
    setNovaEtapa((atual) => ({
      ...atual,
      riscosIds: marcado ? [...atual.riscosIds, id] : atual.riscosIds.filter((r) => r !== id),
    }));
  }

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.aprEtapas.criar(novaEtapa);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar etapa.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    try {
      await api.aprEtapas.excluir(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir etapa.');
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Etapas da atividade</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Ordem">
          <Input
            type="number"
            min={1}
            value={String(novaEtapa.ordem)}
            onChange={(_, d) => setNovaEtapa({ ...novaEtapa, ordem: Math.max(1, Number(d.value) || 1) })}
          />
        </Field>
        <Field label="Descrição da etapa">
          <Input
            value={novaEtapa.descricao}
            onChange={(_, d) => setNovaEtapa({ ...novaEtapa, descricao: d.value })}
          />
        </Field>
        <Field label="Medidas preventivas">
          <Textarea
            value={novaEtapa.medidasPreventivas ?? ''}
            onChange={(_, d) => setNovaEtapa({ ...novaEtapa, medidasPreventivas: d.value })}
          />
        </Field>
      </div>

      <Field label="Riscos desta etapa" style={{ marginBottom: 16 }}>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
          {riscos.map((risco) => (
            <Checkbox
              key={risco.id}
              label={rotuloRisco(risco.id)}
              checked={novaEtapa.riscosIds.includes(risco.id)}
              onChange={(_, d) => alternarRisco(risco.id, !!d.checked)}
            />
          ))}
        </div>
      </Field>

      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Adicionar etapa
        </Button>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Ordem</TableHeaderCell>
            <TableHeaderCell>Descrição</TableHeaderCell>
            <TableHeaderCell>Medidas preventivas</TableHeaderCell>
            <TableHeaderCell>Riscos</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {etapas.map((etapa) => (
            <TableRow key={etapa.id}>
              <TableCell>{etapa.ordem}</TableCell>
              <TableCell>{etapa.descricao}</TableCell>
              <TableCell>{etapa.medidasPreventivas}</TableCell>
              <TableCell>{etapa.riscosIds.map(rotuloRisco).join(', ')}</TableCell>
              <TableCell>
                <Button
                  appearance="subtle"
                  icon={<Delete24Regular />}
                  onClick={() => excluir(etapa.id)}
                  aria-label="Excluir"
                />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
