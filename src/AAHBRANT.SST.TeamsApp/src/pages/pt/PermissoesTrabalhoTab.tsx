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
  type PermissaoTrabalho,
  type Trabalhador,
  type Usuario,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

const ptVazia: NovaPermissaoTrabalho = {
  numeroPt: '',
  atividadeId: '',
  descricaoAtividade: '',
  local: '',
  empresaExecutante: '',
  equipeId: null,
  data: '',
  horarioInicio: null,
  horarioFim: null,
  validade: null,
  responsavelExecucaoUsuarioId: null,
  responsavelAreaUsuarioId: null,
  responsaveisIds: [],
};

const corBadgeStatus: Record<number, 'informative' | 'warning' | 'success' | 'danger' | 'subtle'> = {
  1: 'subtle',
  2: 'success',
  3: 'warning',
  4: 'informative',
};

export function PermissoesTrabalhoTab() {
  const estilos = usePageStyles();
  const navigate = useNavigate();
  const [permissoes, setPermissoes] = useState<PermissaoTrabalho[]>([]);
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [equipes, setEquipes] = useState<Equipe[]>([]);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [novaPt, setNovaPt] = useState<NovaPermissaoTrabalho>(ptVazia);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [lista, ativs, trabs, equips, usrs] = await Promise.all([
        api.permissoesTrabalho.listar(),
        api.atividades.listar(),
        api.trabalhadores.listar(),
        api.equipes.listar(),
        api.usuarios.listar(),
      ]);
      setPermissoes(lista);
      setAtividades(ativs);
      setTrabalhadores(trabs);
      setEquipes(equips);
      setUsuarios(usrs);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar Permissões de Trabalho.');
    } finally {
      setCarregandoLista(false);
    }
  }

  const obraIdAtividadeSelecionada = atividades.find((a) => a.id === novaPt.atividadeId)?.obraId;
  const equipesDaObra = obraIdAtividadeSelecionada
    ? equipes.filter((e) => e.obraId === obraIdAtividadeSelecionada)
    : equipes;

  useEffect(() => {
    carregar();
  }, []);

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
        numeroPt: novaPt.numeroPt || null,
        empresaExecutante: novaPt.empresaExecutante || null,
        equipeId: novaPt.equipeId || null,
        horarioInicio: novaPt.horarioInicio ? `${novaPt.horarioInicio}:00` : null,
        horarioFim: novaPt.horarioFim ? `${novaPt.horarioFim}:00` : null,
        validade: novaPt.validade || null,
        responsavelExecucaoUsuarioId: novaPt.responsavelExecucaoUsuarioId || null,
        responsavelAreaUsuarioId: novaPt.responsavelAreaUsuarioId || null,
      });
      setNovaPt(ptVazia);
      await carregar();
      sucessoToast('Permissão de Trabalho criada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar Permissão de Trabalho.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string, evento: React.MouseEvent) {
    evento.stopPropagation();
    if (!(await confirmar('Excluir esta Permissão de Trabalho? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.permissoesTrabalho.excluir(id);
      await carregar();
      sucessoToast('Permissão de Trabalho excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir Permissão de Trabalho.');
    }
  }

  return (
    <div className={estilos.card}>
      {dialogElement}
      <div className={estilos.toolbar}>
        <Text weight="semibold">Permissão de Trabalho (PT)</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Nº PT">
          <Input value={novaPt.numeroPt ?? ''} onChange={(_, d) => setNovaPt({ ...novaPt, numeroPt: d.value })} />
        </Field>
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
        <Field label="Descrição da atividade">
          <Input
            value={novaPt.descricaoAtividade}
            onChange={(_, d) => setNovaPt({ ...novaPt, descricaoAtividade: d.value })}
          />
        </Field>
        <Field label="Local">
          <Input value={novaPt.local} onChange={(_, d) => setNovaPt({ ...novaPt, local: d.value })} />
        </Field>
        <Field label="Empresa executante">
          <Input
            value={novaPt.empresaExecutante ?? ''}
            onChange={(_, d) => setNovaPt({ ...novaPt, empresaExecutante: d.value })}
          />
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
        <Field label="Responsável pela execução">
          <Select
            value={novaPt.responsavelExecucaoUsuarioId ?? ''}
            onChange={(_, d) => setNovaPt({ ...novaPt, responsavelExecucaoUsuarioId: d.value || null })}
          >
            <option value="">Não definido</option>
            {usuarios.map((usuario) => (
              <option key={usuario.id} value={usuario.id}>
                {usuario.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Responsável pela área">
          <Select
            value={novaPt.responsavelAreaUsuarioId ?? ''}
            onChange={(_, d) => setNovaPt({ ...novaPt, responsavelAreaUsuarioId: d.value || null })}
          >
            <option value="">Não definido</option>
            {usuarios.map((usuario) => (
              <option key={usuario.id} value={usuario.id}>
                {usuario.nome}
              </option>
            ))}
          </Select>
        </Field>
      </div>

      <Field label="Equipe executante (responsáveis)" style={{ marginBottom: 16 }}>
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
        <Button
          appearance="primary"
          icon={<Add24Regular />}
          onClick={criar}
          disabled={carregando || !novaPt.atividadeId || !novaPt.descricaoAtividade || !novaPt.local || !novaPt.data}
        >
          Adicionar PT
        </Button>
      </div>

      {carregandoLista ? (
        <ListaCarregando />
      ) : permissoes.length === 0 ? (
        <EstadoVazio mensagem="Nenhuma Permissão de Trabalho cadastrada ainda." />
      ) : (
      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Nº PT</TableHeaderCell>
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
              <TableCell>{pt.numeroPt ?? '-'}</TableCell>
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
      )}
    </div>
  );
}
