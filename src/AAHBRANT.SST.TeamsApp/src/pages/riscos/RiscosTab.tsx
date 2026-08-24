import { useEffect, useState } from 'react';
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
  Textarea,
} from '@fluentui/react-components';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import {
  api,
  nivelRiscoLabel,
  StatusControleRisco,
  statusControleRiscoLabel,
  type Atividade,
  type NovoRisco,
  type Perigo,
  type Risco,
  type Trabalhador,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

const riscoVazio: NovoRisco = {
  atividadeId: '',
  perigoId: '',
  ambiente: '',
  exposicao: '',
  consequencia: '',
  probabilidade: 1,
  severidade: 1,
  controlesExistentes: '',
  controlesAdicionais: '',
  responsavelUsuarioId: null,
  prazo: null,
  status: StatusControleRisco.Pendente,
  trabalhadoresExpostosIds: [],
};

const corBadgeNivel: Record<number, 'success' | 'informative' | 'warning' | 'severe' | 'danger'> = {
  1: 'success',
  2: 'informative',
  3: 'warning',
  4: 'severe',
  5: 'danger',
};

export function RiscosTab() {
  const estilos = usePageStyles();
  const [riscos, setRiscos] = useState<Risco[]>([]);
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [perigos, setPerigos] = useState<Perigo[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [novoRisco, setNovoRisco] = useState<NovoRisco>(riscoVazio);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [rscs, ativs, prgs, trabs] = await Promise.all([
        api.riscos.listar(),
        api.atividades.listar(),
        api.perigos.listar(),
        api.trabalhadores.listar(),
      ]);
      setRiscos(rscs);
      setAtividades(ativs);
      setPerigos(prgs);
      setTrabalhadores(trabs);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar avaliações de risco.');
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function nomeAtividade(id: string) {
    return atividades.find((a) => a.id === id)?.nome ?? id;
  }

  function nomePerigo(id: string) {
    return perigos.find((p) => p.id === id)?.nome ?? id;
  }

  function alternarTrabalhadorExposto(id: string, marcado: boolean) {
    setNovoRisco((atual) => ({
      ...atual,
      trabalhadoresExpostosIds: marcado
        ? [...atual.trabalhadoresExpostosIds, id]
        : atual.trabalhadoresExpostosIds.filter((t) => t !== id),
    }));
  }

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.riscos.criar(novoRisco);
      setNovoRisco(riscoVazio);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar avaliação de risco.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    try {
      await api.riscos.excluir(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir avaliação de risco.');
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Avaliações de risco</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Atividade">
          <Select
            value={novoRisco.atividadeId}
            onChange={(_, d) => setNovoRisco({ ...novoRisco, atividadeId: d.value })}
          >
            <option value="">Selecione</option>
            {atividades.map((atividade) => (
              <option key={atividade.id} value={atividade.id}>
                {atividade.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Perigo">
          <Select value={novoRisco.perigoId} onChange={(_, d) => setNovoRisco({ ...novoRisco, perigoId: d.value })}>
            <option value="">Selecione</option>
            {perigos.map((perigo) => (
              <option key={perigo.id} value={perigo.id}>
                {perigo.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Ambiente">
          <Input
            value={novoRisco.ambiente ?? ''}
            onChange={(_, d) => setNovoRisco({ ...novoRisco, ambiente: d.value })}
          />
        </Field>
        <Field label="Exposição">
          <Input
            value={novoRisco.exposicao ?? ''}
            onChange={(_, d) => setNovoRisco({ ...novoRisco, exposicao: d.value })}
          />
        </Field>
        <Field label="Consequência">
          <Input
            value={novoRisco.consequencia ?? ''}
            onChange={(_, d) => setNovoRisco({ ...novoRisco, consequencia: d.value })}
          />
        </Field>
        <Field label="Probabilidade">
          <Input
            type="number"
            min={1}
            value={String(novoRisco.probabilidade)}
            onChange={(_, d) => setNovoRisco({ ...novoRisco, probabilidade: Math.max(1, Number(d.value) || 1) })}
          />
        </Field>
        <Field label="Severidade">
          <Input
            type="number"
            min={1}
            value={String(novoRisco.severidade)}
            onChange={(_, d) => setNovoRisco({ ...novoRisco, severidade: Math.max(1, Number(d.value) || 1) })}
          />
        </Field>
        <Field label="Prazo">
          <Input
            type="date"
            value={novoRisco.prazo ?? ''}
            onChange={(_, d) => setNovoRisco({ ...novoRisco, prazo: d.value || null })}
          />
        </Field>
        <Field label="Status">
          <Select
            value={novoRisco.status}
            onChange={(_, d) => setNovoRisco({ ...novoRisco, status: Number(d.value) })}
          >
            {Object.entries(statusControleRiscoLabel).map(([valor, rotulo]) => (
              <option key={valor} value={valor}>
                {rotulo}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Controles existentes">
          <Textarea
            value={novoRisco.controlesExistentes ?? ''}
            onChange={(_, d) => setNovoRisco({ ...novoRisco, controlesExistentes: d.value })}
          />
        </Field>
        <Field label="Controles adicionais">
          <Textarea
            value={novoRisco.controlesAdicionais ?? ''}
            onChange={(_, d) => setNovoRisco({ ...novoRisco, controlesAdicionais: d.value })}
          />
        </Field>
      </div>

      <Field label="Trabalhadores expostos" style={{ marginBottom: 16 }}>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
          {trabalhadores.map((trabalhador) => (
            <Checkbox
              key={trabalhador.id}
              label={trabalhador.nome}
              checked={novoRisco.trabalhadoresExpostosIds.includes(trabalhador.id)}
              onChange={(_, d) => alternarTrabalhadorExposto(trabalhador.id, !!d.checked)}
            />
          ))}
        </div>
      </Field>

      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Registrar avaliação de risco
        </Button>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Atividade</TableHeaderCell>
            <TableHeaderCell>Perigo</TableHeaderCell>
            <TableHeaderCell>P × S</TableHeaderCell>
            <TableHeaderCell>Nível de risco</TableHeaderCell>
            <TableHeaderCell>Status</TableHeaderCell>
            <TableHeaderCell>Expostos</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {riscos.map((risco) => (
            <TableRow key={risco.id}>
              <TableCell>{nomeAtividade(risco.atividadeId)}</TableCell>
              <TableCell>{nomePerigo(risco.perigoId)}</TableCell>
              <TableCell>
                {risco.probabilidade} × {risco.severidade}
              </TableCell>
              <TableCell>
                <Badge color={corBadgeNivel[risco.nivelRisco]} appearance="tint">
                  {nivelRiscoLabel[risco.nivelRisco]}
                </Badge>
              </TableCell>
              <TableCell>{statusControleRiscoLabel[risco.status]}</TableCell>
              <TableCell>{risco.trabalhadoresExpostosIds.length}</TableCell>
              <TableCell>
                <Button
                  appearance="subtle"
                  icon={<Delete24Regular />}
                  onClick={() => excluir(risco.id)}
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
