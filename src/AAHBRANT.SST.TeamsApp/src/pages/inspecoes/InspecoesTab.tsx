import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Badge,
  Button,
  Field,
  Select,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
} from '@fluentui/react-components';
import { CampoData } from '../../components/CampoData';
import { Add24Regular, ChevronRight24Regular } from '@fluentui/react-icons';
import {
  api,
  statusInspecaoLabel,
  tipoInspecaoLabel,
  type Atividade,
  type ChecklistModelo,
  type Inspecao,
  type NovaInspecao,
  type Obra,
  type Usuario,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

const inspecaoVazia: NovaInspecao = {
  checklistModeloId: '',
  obraId: '',
  atividadeId: null,
  data: '',
  responsavelUsuarioId: '',
};

const corBadgeStatus: Record<number, 'informative' | 'success'> = {
  1: 'informative',
  2: 'success',
};

export function InspecoesTab() {
  const estilos = usePageStyles();
  const navigate = useNavigate();
  const [inspecoes, setInspecoes] = useState<Inspecao[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [checklists, setChecklists] = useState<ChecklistModelo[]>([]);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [novaInspecao, setNovaInspecao] = useState<NovaInspecao>(inspecaoVazia);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [lista, listaObras, listaAtividades, listaChecklists, listaUsuarios] = await Promise.all([
        api.inspecoes.listar(),
        api.obras.listar(),
        api.atividades.listar(),
        api.checklistModelos.listar(),
        api.usuarios.listar(),
      ]);
      setInspecoes(lista);
      setObras(listaObras);
      setAtividades(listaAtividades);
      setChecklists(listaChecklists);
      setUsuarios(listaUsuarios);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar inspeções.');
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  const atividadesDaObra = atividades.filter((a) => a.obraId === novaInspecao.obraId);

  async function criar() {
    if (!novaInspecao.checklistModeloId || !novaInspecao.obraId || !novaInspecao.data || !novaInspecao.responsavelUsuarioId) {
      setErro('Preencha checklist, obra, data e responsável.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.inspecoes.criar({
        ...novaInspecao,
        atividadeId: novaInspecao.atividadeId || null,
      });
      setNovaInspecao(inspecaoVazia);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar inspeção.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Execuções de inspeção</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Checklist">
          <Select
            value={novaInspecao.checklistModeloId}
            onChange={(_, d) => setNovaInspecao({ ...novaInspecao, checklistModeloId: d.value })}
          >
            <option value="">Selecione</option>
            {checklists.map((c) => (
              <option key={c.id} value={c.id}>
                {c.nome} (v{c.versao} — {tipoInspecaoLabel[c.tipoInspecao]})
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Obra">
          <Select
            value={novaInspecao.obraId}
            onChange={(_, d) => setNovaInspecao({ ...novaInspecao, obraId: d.value, atividadeId: null })}
          >
            <option value="">Selecione</option>
            {obras.map((obra) => (
              <option key={obra.id} value={obra.id}>
                {obra.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Atividade (opcional)">
          <Select
            value={novaInspecao.atividadeId ?? ''}
            onChange={(_, d) => setNovaInspecao({ ...novaInspecao, atividadeId: d.value || null })}
            disabled={!novaInspecao.obraId}
          >
            <option value="">Nenhuma</option>
            {atividadesDaObra.map((atividade) => (
              <option key={atividade.id} value={atividade.id}>
                {atividade.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Data">
          <CampoData
            value={novaInspecao.data}
            onChange={(_, d) => setNovaInspecao({ ...novaInspecao, data: d.value })}
          />
        </Field>
        <Field label="Responsável">
          <Select
            value={novaInspecao.responsavelUsuarioId}
            onChange={(_, d) => setNovaInspecao({ ...novaInspecao, responsavelUsuarioId: d.value })}
          >
            <option value="">Selecione</option>
            {usuarios.map((usuario) => (
              <option key={usuario.id} value={usuario.id}>
                {usuario.nome}
              </option>
            ))}
          </Select>
        </Field>
      </div>

      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Iniciar inspeção
        </Button>
      </div>

      <Table noNativeElements>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Tipo</TableHeaderCell>
            <TableHeaderCell>Obra</TableHeaderCell>
            <TableHeaderCell>Checklist</TableHeaderCell>
            <TableHeaderCell>Data</TableHeaderCell>
            <TableHeaderCell>Responsável</TableHeaderCell>
            <TableHeaderCell>Progresso</TableHeaderCell>
            <TableHeaderCell>Status</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {inspecoes.map((inspecao) => (
            <TableRow
              key={inspecao.id}
              onClick={() => navigate(`/prevencao/inspecoes/${inspecao.id}`)}
              style={{ cursor: 'pointer' }}
            >
              <TableCell>{tipoInspecaoLabel[inspecao.tipoInspecao]}</TableCell>
              <TableCell>{inspecao.obraNome}</TableCell>
              <TableCell>
                {inspecao.checklistModeloNome} (v{inspecao.checklistModeloVersao})
              </TableCell>
              <TableCell>{inspecao.data?.slice(0, 10)}</TableCell>
              <TableCell>{inspecao.responsavelUsuarioNome}</TableCell>
              <TableCell>
                {inspecao.itensRespondidos}/{inspecao.totalItens}
                {inspecao.itensNaoConformes > 0 && (
                  <Badge color="danger" appearance="tint" style={{ marginLeft: 6 }}>
                    {inspecao.itensNaoConformes} NC
                  </Badge>
                )}
              </TableCell>
              <TableCell>
                <Badge color={corBadgeStatus[inspecao.status]} appearance="tint">
                  {statusInspecaoLabel[inspecao.status]}
                </Badge>
              </TableCell>
              <TableCell>
                <Button
                  appearance="subtle"
                  icon={<ChevronRight24Regular />}
                  onClick={() => navigate(`/prevencao/inspecoes/${inspecao.id}`)}
                  aria-label="Ver inspeção"
                />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
