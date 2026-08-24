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
import {
  api,
  statusPtLabel,
  type Atividade,
  type Equipe,
  type NovaPermissaoTrabalho,
  type Perigo,
  type PermissaoTrabalho,
  type Trabalhador,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

const ptVazia: NovaPermissaoTrabalho = {
  atividadeId: '',
  local: '',
  equipeId: null,
  data: '',
  horarioInicio: null,
  horarioFim: null,
  validade: null,
  perigosIds: [],
  responsaveisIds: [],
};

const corBadgeStatus: Record<number, 'informative' | 'warning' | 'success' | 'danger' | 'subtle'> = {
  1: 'subtle',
  2: 'success',
  3: 'informative',
};

export function PermissoesTrabalhoTab() {
  const estilos = usePageStyles();
  const navigate = useNavigate();
  const [permissoes, setPermissoes] = useState<PermissaoTrabalho[]>([]);
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [perigos, setPerigos] = useState<Perigo[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [equipes, setEquipes] = useState<Equipe[]>([]);
  const [novaPt, setNovaPt] = useState<NovaPermissaoTrabalho>(ptVazia);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [lista, ativs, prgs, trabs, equips] = await Promise.all([
        api.permissoesTrabalho.listar(),
        api.atividades.listar(),
        api.perigos.listar(),
        api.trabalhadores.listar(),
        api.equipes.listar(),
      ]);
      setPermissoes(lista);
      setAtividades(ativs);
      setPerigos(prgs);
      setTrabalhadores(trabs);
      setEquipes(equips);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar Permissões de Trabalho.');
    }
  }

  const obraIdAtividadeSelecionada = atividades.find((a) => a.id === novaPt.atividadeId)?.obraId;
  const equipesDaObra = obraIdAtividadeSelecionada
    ? equipes.filter((e) => e.obraId === obraIdAtividadeSelecionada)
    : equipes;

  useEffect(() => {
    carregar();
  }, []);

  function alternarPerigo(id: string, marcado: boolean) {
    setNovaPt((atual) => ({
      ...atual,
      perigosIds: marcado ? [...atual.perigosIds, id] : atual.perigosIds.filter((p) => p !== id),
    }));
  }

  function alternarResponsavel(id: string, marcado: boolean) {
    setNovaPt((atual) => ({
      ...atual,
      responsaveisIds: marcado ? [...atual.responsaveisIds, id] : atual.responsaveisIds.filter((r) => r !== id),
    }));
  }

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.permissoesTrabalho.criar({
        ...novaPt,
        equipeId: novaPt.equipeId || null,
        horarioInicio: novaPt.horarioInicio ? `${novaPt.horarioInicio}:00` : null,
        horarioFim: novaPt.horarioFim ? `${novaPt.horarioFim}:00` : null,
        validade: novaPt.validade || null,
      });
      setNovaPt(ptVazia);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar Permissão de Trabalho.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string, evento: React.MouseEvent) {
    evento.stopPropagation();
    try {
      await api.permissoesTrabalho.excluir(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir Permissão de Trabalho.');
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Permissão de Trabalho (PT)</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Atividade">
          <Select value={novaPt.atividadeId} onChange={(_, d) => setNovaPt({ ...novaPt, atividadeId: d.value })}>
            <option value="">Selecione</option>
            {atividades.map((atividade) => (
              <option key={atividade.id} value={atividade.id}>
                {atividade.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Local">
          <Input value={novaPt.local} onChange={(_, d) => setNovaPt({ ...novaPt, local: d.value })} />
        </Field>
        <Field label="Equipe">
          <Select
            value={novaPt.equipeId ?? ''}
            onChange={(_, d) => setNovaPt({ ...novaPt, equipeId: d.value || null })}
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
          <Input type="date" value={novaPt.data} onChange={(_, d) => setNovaPt({ ...novaPt, data: d.value })} />
        </Field>
        <Field label="Horário início">
          <Input
            type="time"
            value={novaPt.horarioInicio ?? ''}
            onChange={(_, d) => setNovaPt({ ...novaPt, horarioInicio: d.value || null })}
          />
        </Field>
        <Field label="Horário fim">
          <Input
            type="time"
            value={novaPt.horarioFim ?? ''}
            onChange={(_, d) => setNovaPt({ ...novaPt, horarioFim: d.value || null })}
          />
        </Field>
        <Field label="Validade">
          <Input
            type="date"
            value={novaPt.validade ?? ''}
            onChange={(_, d) => setNovaPt({ ...novaPt, validade: d.value || null })}
          />
        </Field>
      </div>

      <Field label="Perigos" style={{ marginBottom: 16 }}>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
          {perigos.map((perigo) => (
            <Checkbox
              key={perigo.id}
              label={perigo.nome}
              checked={novaPt.perigosIds.includes(perigo.id)}
              onChange={(_, d) => alternarPerigo(perigo.id, !!d.checked)}
            />
          ))}
        </div>
      </Field>

      <Field label="Responsáveis" style={{ marginBottom: 16 }}>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
          {trabalhadores.map((trabalhador) => (
            <Checkbox
              key={trabalhador.id}
              label={trabalhador.nome}
              checked={novaPt.responsaveisIds.includes(trabalhador.id)}
              onChange={(_, d) => alternarResponsavel(trabalhador.id, !!d.checked)}
            />
          ))}
        </div>
      </Field>

      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Adicionar PT
        </Button>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Atividade</TableHeaderCell>
            <TableHeaderCell>Local</TableHeaderCell>
            <TableHeaderCell>Data</TableHeaderCell>
            <TableHeaderCell>Validade</TableHeaderCell>
            <TableHeaderCell>Status</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {permissoes.map((pt) => (
            <TableRow key={pt.id} onClick={() => navigate(`/operacao/pt/${pt.id}`)} style={{ cursor: 'pointer' }}>
              <TableCell>{pt.atividadeNome}</TableCell>
              <TableCell>{pt.local}</TableCell>
              <TableCell>{pt.data?.slice(0, 10)}</TableCell>
              <TableCell>{pt.validade?.slice(0, 10)}</TableCell>
              <TableCell>
                <Badge color={corBadgeStatus[pt.status]} appearance="tint">
                  {statusPtLabel[pt.status]}
                </Badge>
              </TableCell>
              <TableCell>
                <div style={{ display: 'flex', gap: 4 }}>
                  <Button
                    appearance="subtle"
                    icon={<ChevronRight24Regular />}
                    onClick={() => navigate(`/operacao/pt/${pt.id}`)}
                    aria-label="Ver PT"
                  />
                  <Button
                    appearance="subtle"
                    icon={<Delete24Regular />}
                    onClick={(evento) => excluir(pt.id, evento)}
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
