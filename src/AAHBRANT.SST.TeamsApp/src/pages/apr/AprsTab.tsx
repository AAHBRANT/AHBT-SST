import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Badge,
  Button,
  Checkbox,
  Field,
  Input,
  Select,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
} from '@fluentui/react-components';
import { Add24Regular, ChevronRight24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, statusAprLabel, type Apr, type Atividade, type Equipe, type NovaApr, type Trabalhador } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

const aprVazia: NovaApr = {
  numeroApr: '',
  atividadeId: '',
  local: '',
  maquinasEquipamentos: '',
  pgrReferencia: '',
  equipeId: null,
  data: '',
  validade: null,
  responsaveisIds: [],
};

const corBadgeStatus: Record<number, 'informative' | 'warning' | 'success' | 'danger' | 'subtle'> = {
  1: 'subtle',
  2: 'warning',
  3: 'success',
  4: 'danger',
  5: 'informative',
};

export function AprsTab() {
  const estilos = usePageStyles();
  const navigate = useNavigate();
  const [aprs, setAprs] = useState<Apr[]>([]);
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [equipes, setEquipes] = useState<Equipe[]>([]);
  const [novaApr, setNovaApr] = useState<NovaApr>(aprVazia);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [lista, ativs, trabs, equips] = await Promise.all([
        api.aprs.listar(),
        api.atividades.listar(),
        api.trabalhadores.listar(),
        api.equipes.listar(),
      ]);
      setAprs(lista);
      setAtividades(ativs);
      setTrabalhadores(trabs);
      setEquipes(equips);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar APRs.');
    }
  }

  const obraIdAtividadeSelecionada = atividades.find((a) => a.id === novaApr.atividadeId)?.obraId;
  const equipesDaObra = obraIdAtividadeSelecionada
    ? equipes.filter((e) => e.obraId === obraIdAtividadeSelecionada)
    : equipes;

  useEffect(() => {
    carregar();
  }, []);

  function alternarResponsavel(id: string, marcado: boolean) {
    setNovaApr((atual) => ({
      ...atual,
      responsaveisIds: marcado
        ? [...atual.responsaveisIds, id]
        : atual.responsaveisIds.filter((r) => r !== id),
    }));
  }

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.aprs.criar({
        ...novaApr,
        validade: novaApr.validade || null,
      });
      setNovaApr(aprVazia);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar APR.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string, evento: React.MouseEvent) {
    evento.stopPropagation();
    try {
      await api.aprs.excluir(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir APR.');
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Análise Preliminar de Risco (APR)</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Nº APR">
          <Input value={novaApr.numeroApr ?? ''} onChange={(_, d) => setNovaApr({ ...novaApr, numeroApr: d.value })} />
        </Field>
        <Field label="Atividade">
          <Select
            value={novaApr.atividadeId}
            onChange={(_, d) => setNovaApr({ ...novaApr, atividadeId: d.value })}
          >
            <option value="">Selecione</option>
            {atividades.map((atividade) => (
              <option key={atividade.id} value={atividade.id}>
                {atividade.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Local / Frente">
          <Input value={novaApr.local} onChange={(_, d) => setNovaApr({ ...novaApr, local: d.value })} />
        </Field>
        <Field label="Máquinas / Equip.">
          <Input
            value={novaApr.maquinasEquipamentos ?? ''}
            onChange={(_, d) => setNovaApr({ ...novaApr, maquinasEquipamentos: d.value })}
          />
        </Field>
        <Field label="PGR / Procedimento ref.">
          <Input
            value={novaApr.pgrReferencia ?? ''}
            onChange={(_, d) => setNovaApr({ ...novaApr, pgrReferencia: d.value })}
          />
        </Field>
        <Field label="Equipe">
          <Select
            value={novaApr.equipeId ?? ''}
            onChange={(_, d) => setNovaApr({ ...novaApr, equipeId: d.value || null })}
          >
            <option value="">Nenhuma</option>
            {equipesDaObra.map((equipe) => (
              <option key={equipe.id} value={equipe.id}>
                {equipe.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Data">
          <Input
            type="date"
            value={novaApr.data}
            onChange={(_, d) => setNovaApr({ ...novaApr, data: d.value })}
          />
        </Field>
        <Field label="Validade">
          <Input
            type="date"
            value={novaApr.validade ?? ''}
            onChange={(_, d) => setNovaApr({ ...novaApr, validade: d.value || null })}
          />
        </Field>
      </div>

      <Field label="Responsáveis" style={{ marginBottom: 16 }}>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
          {trabalhadores.map((trabalhador) => (
            <Checkbox
              key={trabalhador.id}
              label={trabalhador.nome}
              checked={novaApr.responsaveisIds.includes(trabalhador.id)}
              onChange={(_, d) => alternarResponsavel(trabalhador.id, !!d.checked)}
            />
          ))}
        </div>
      </Field>

      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Adicionar APR
        </Button>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Nº APR</TableHeaderCell>
            <TableHeaderCell>Atividade</TableHeaderCell>
            <TableHeaderCell>Local</TableHeaderCell>
            <TableHeaderCell>Data</TableHeaderCell>
            <TableHeaderCell>Validade</TableHeaderCell>
            <TableHeaderCell>Status</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {aprs.map((apr) => (
            <TableRow key={apr.id} onClick={() => navigate(`/operacao/apr/${apr.id}`)} style={{ cursor: 'pointer' }}>
              <TableCell>{apr.numeroApr ?? '-'}</TableCell>
              <TableCell>{apr.atividadeNome}</TableCell>
              <TableCell>{apr.local}</TableCell>
              <TableCell>{apr.data?.slice(0, 10)}</TableCell>
              <TableCell>{apr.validade?.slice(0, 10)}</TableCell>
              <TableCell>
                <Badge color={corBadgeStatus[apr.status]} appearance="tint">
                  {statusAprLabel[apr.status]}
                </Badge>
              </TableCell>
              <TableCell>
                <div style={{ display: 'flex', gap: 4 }}>
                  <Button
                    appearance="subtle"
                    icon={<ChevronRight24Regular />}
                    onClick={() => navigate(`/operacao/apr/${apr.id}`)}
                    aria-label="Ver APR"
                  />
                  <Button
                    appearance="subtle"
                    icon={<Delete24Regular />}
                    onClick={(evento) => excluir(apr.id, evento)}
                    aria-label="Excluir"
                  />
                </div>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
