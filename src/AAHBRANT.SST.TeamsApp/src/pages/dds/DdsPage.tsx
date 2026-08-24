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
import { Add24Regular, ChevronRight24Regular } from '@fluentui/react-icons';
import {
  api,
  statusDdsLabel,
  StatusDds,
  type Atividade,
  type Dds,
  type NovaDds,
  type Obra,
  type Usuario,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

function ddsVazia(): NovaDds {
  return { obraId: '', atividadesIds: [], data: '', responsavelUsuarioId: '' };
}

const corBadgeStatus: Record<number, 'informative' | 'success'> = {
  [StatusDds.EmAndamento]: 'informative',
  [StatusDds.Concluido]: 'success',
};

// Módulo pedido pelo usuário em 20/08 (ver memória project_sst_gsst_ia_aprovada), fora do MVP
// da §47 — o roteiro (tópico + checklist) é gerado pelo backend a partir das Atividades
// selecionadas aqui, cruzando com os Riscos já cadastrados. Ver CriarDdsCommand.
export function DdsPage() {
  const estilos = usePageStyles();
  const navigate = useNavigate();
  const [registros, setRegistros] = useState<Dds[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [novaDds, setNovaDds] = useState<NovaDds>(ddsVazia());
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [lista, listaObras, listaAtividades, listaUsuarios] = await Promise.all([
        api.dds.listar(),
        api.obras.listar(),
        api.atividades.listar(),
        api.usuarios.listar(),
      ]);
      setRegistros(lista);
      setObras(listaObras);
      setAtividades(listaAtividades);
      setUsuarios(listaUsuarios);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar DDS.');
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  const atividadesDaObra = atividades.filter((a) => a.obraId === novaDds.obraId);

  function alternarAtividade(id: string, marcado: boolean) {
    setNovaDds((atual) => ({
      ...atual,
      atividadesIds: marcado ? [...atual.atividadesIds, id] : atual.atividadesIds.filter((a) => a !== id),
    }));
  }

  async function criar() {
    if (!novaDds.obraId || novaDds.atividadesIds.length === 0 || !novaDds.data || !novaDds.responsavelUsuarioId) {
      setErro('Preencha obra, ao menos uma atividade, data e responsável.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.dds.criar(novaDds);
      setNovaDds(ddsVazia());
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar DDS.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <div>
      <div style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          DDS — Diálogo Diário de Segurança
        </Text>
      </div>

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Novo DDS</Text>
        </div>

        {erro && <Text className={estilos.erro}>{erro}</Text>}

        <div className={estilos.form}>
          <Field label="Obra">
            <Select
              value={novaDds.obraId}
              onChange={(_, d) => setNovaDds({ ...novaDds, obraId: d.value, atividadesIds: [] })}
            >
              <option value="">Selecione</option>
              {obras.map((obra) => (
                <option key={obra.id} value={obra.id}>
                  {obra.nome}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Data">
            <Input type="date" value={novaDds.data} onChange={(_, d) => setNovaDds({ ...novaDds, data: d.value })} />
          </Field>
          <Field label="Responsável">
            <Select
              value={novaDds.responsavelUsuarioId}
              onChange={(_, d) => setNovaDds({ ...novaDds, responsavelUsuarioId: d.value })}
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

        <Field label="Atividades do dia" style={{ marginTop: 12 }}>
          {!novaDds.obraId ? (
            <Text>Selecione uma obra para ver as atividades.</Text>
          ) : atividadesDaObra.length === 0 ? (
            <Text>Nenhuma atividade cadastrada para esta obra.</Text>
          ) : (
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
              {atividadesDaObra.map((atividade) => (
                <Checkbox
                  key={atividade.id}
                  label={atividade.nome}
                  checked={novaDds.atividadesIds.includes(atividade.id)}
                  onChange={(_, d) => alternarAtividade(atividade.id, !!d.checked)}
                />
              ))}
            </div>
          )}
        </Field>

        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
            Gerar roteiro do DDS
          </Button>
        </div>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">DDS registrados</Text>
        </div>

        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Obra</TableHeaderCell>
              <TableHeaderCell>Data</TableHeaderCell>
              <TableHeaderCell>Tópico principal</TableHeaderCell>
              <TableHeaderCell>Responsável</TableHeaderCell>
              <TableHeaderCell>Checklist</TableHeaderCell>
              <TableHeaderCell>Participantes</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {registros.map((dds) => (
              <TableRow key={dds.id} onClick={() => navigate(`/prevencao/dds/${dds.id}`)} style={{ cursor: 'pointer' }}>
                <TableCell>{dds.obraNome}</TableCell>
                <TableCell>{dds.data?.slice(0, 10)}</TableCell>
                <TableCell>{dds.topicoPrincipal}</TableCell>
                <TableCell>{dds.responsavelUsuarioNome}</TableCell>
                <TableCell>
                  {dds.itensVerificados}/{dds.totalItensChecklist}
                </TableCell>
                <TableCell>{dds.totalParticipantes}</TableCell>
                <TableCell>
                  <Badge color={corBadgeStatus[dds.status]} appearance="tint">
                    {statusDdsLabel[dds.status]}
                  </Badge>
                </TableCell>
                <TableCell>
                  <Button
                    appearance="subtle"
                    icon={<ChevronRight24Regular />}
                    onClick={() => navigate(`/prevencao/dds/${dds.id}`)}
                    aria-label="Ver DDS"
                  />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
