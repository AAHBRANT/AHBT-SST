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
  Textarea,
} from '@fluentui/react-components';
import { CampoData } from '../../components/CampoData';
import { Add24Regular, ChevronRight24Regular, Delete24Regular } from '@fluentui/react-icons';
import {
  api,
  statusReuniaoCipaLabel,
  tipoReuniaoCipaLabel,
  TipoReuniaoCipa,
  type NovaReuniaoCipa,
  type Obra,
  type ReuniaoCipa,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

function vazio(): NovaReuniaoCipa {
  return { obraId: '', tipo: TipoReuniaoCipa.Ordinaria, dataReuniao: '', pauta: '' };
}

export function ReunioesCipaTab() {
  const estilos = usePageStyles();
  const navigate = useNavigate();
  const [lista, setLista] = useState<ReuniaoCipa[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [novo, setNovo] = useState<NovaReuniaoCipa>(vazio());
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [listaReunioes, listaObras] = await Promise.all([api.cipa.reunioes.listar(), api.obras.listar()]);
      setLista(listaReunioes);
      setObras(listaObras);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar reuniões da CIPA.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function nomeObra(id: string) {
    return obras.find((o) => o.id === id)?.nome ?? id;
  }

  async function criar() {
    if (!novo.obraId || !novo.dataReuniao) {
      setErro('Preencha obra e data da reunião.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.cipa.reunioes.criar({ ...novo, pauta: novo.pauta || null });
      setNovo(vazio());
      await carregar();
      sucessoToast('Reunião agendada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar reunião.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string, evento: React.MouseEvent) {
    evento.stopPropagation();
    if (!(await confirmar('Excluir esta reunião da CIPA? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.cipa.reunioes.excluir(id);
      await carregar();
      sucessoToast('Reunião excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir reunião.');
    }
  }

  return (
    <div>
      {dialogElement}
      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Agendar reunião</Text>
        </div>
        {erro && <Text className={estilos.erro}>{erro}</Text>}
        <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>Dados da Reunião</div>
        <div className={estilos.formGrid}>
          <div className={estilos.col4}>
            <Field label="Obra" required>
              <Select value={novo.obraId} onChange={(_, d) => setNovo({ ...novo, obraId: d.value })}>
                <option value="">Selecione</option>
                {obras.map((obra) => (
                  <option key={obra.id} value={obra.id}>
                    {obra.nome}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <div className={estilos.col3}>
            <Field label="Tipo">
              <Select value={String(novo.tipo)} onChange={(_, d) => setNovo({ ...novo, tipo: Number(d.value) })}>
                {Object.entries(tipoReuniaoCipaLabel).map(([valor, rotulo]) => (
                  <option key={valor} value={valor}>
                    {rotulo}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <div className={estilos.col3}>
            <Field label="Data da reunião" required>
              <CampoData value={novo.dataReuniao} onChange={(_, d) => setNovo({ ...novo, dataReuniao: d.value })} />
            </Field>
          </div>
          <div className={estilos.col12}>
            <Field label="Pauta">
              <Textarea value={novo.pauta ?? ''} onChange={(_, d) => setNovo({ ...novo, pauta: d.value })} />
            </Field>
          </div>
        </div>
        <div className={estilos.footer}>
          <Text className={estilos.footerInfo}>
            Lista de presença, deliberações e o plano de ações (matriz 5W2H) da reunião são registrados
            na tela de detalhe.
          </Text>
          <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
            Agendar reunião
          </Button>
        </div>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Reuniões</Text>
        </div>
        {carregandoLista ? (
          <ListaCarregando />
        ) : lista.length === 0 ? (
          <EstadoVazio mensagem="Nenhuma reunião da CIPA cadastrada ainda." />
        ) : (
        <Table noNativeElements>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Obra</TableHeaderCell>
              <TableHeaderCell>Tipo</TableHeaderCell>
              <TableHeaderCell>Data</TableHeaderCell>
              <TableHeaderCell>Presentes</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {lista.map((r) => (
              <TableRow key={r.id} onClick={() => navigate(`/operacao/cipa/reuniao/${r.id}`)} style={{ cursor: 'pointer' }}>
                <TableCell>{nomeObra(r.obraId)}</TableCell>
                <TableCell>{tipoReuniaoCipaLabel[r.tipo]}</TableCell>
                <TableCell>{r.dataReuniao?.slice(0, 10)}</TableCell>
                <TableCell>
                  {r.totalPresentes}/{r.totalParticipantes}
                </TableCell>
                <TableCell>
                  <Badge appearance="tint">{statusReuniaoCipaLabel[r.status]}</Badge>
                </TableCell>
                <TableCell>
                  <div style={{ display: 'flex', gap: 4 }}>
                    <Button
                      appearance="subtle"
                      icon={<ChevronRight24Regular />}
                      onClick={() => navigate(`/operacao/cipa/reuniao/${r.id}`)}
                      aria-label="Ver reunião"
                    />
                    <Button appearance="subtle" icon={<Delete24Regular />} onClick={(e) => excluir(r.id, e)} aria-label="Excluir" />
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        )}
      </div>
    </div>
  );
}
