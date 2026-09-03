import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button,
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
import { CampoData } from '../../components/CampoData';
import { Add24Regular, ChevronRight24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, type EventoSipat, type NovoEventoSipat, type Obra } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

function vazio(): NovoEventoSipat {
  return { obraId: '', anoReferencia: new Date().getFullYear(), dataInicio: '', dataFim: '', tema: '', programacao: '' };
}

export function SipatTab() {
  const estilos = usePageStyles();
  const navigate = useNavigate();
  const [lista, setLista] = useState<EventoSipat[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [novo, setNovo] = useState<NovoEventoSipat>(vazio());
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [listaEventos, listaObras] = await Promise.all([api.cipa.eventosSipat.listar(), api.obras.listar()]);
      setLista(listaEventos);
      setObras(listaObras);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar eventos SIPAT.');
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
    if (!novo.obraId || !novo.dataInicio || !novo.dataFim) {
      setErro('Preencha obra e o período do evento.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.cipa.eventosSipat.criar({ ...novo, tema: novo.tema || null, programacao: novo.programacao || null });
      setNovo(vazio());
      await carregar();
      sucessoToast('Evento SIPAT criado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar evento SIPAT.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string, evento: React.MouseEvent) {
    evento.stopPropagation();
    if (!(await confirmar('Excluir este evento SIPAT? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.cipa.eventosSipat.excluir(id);
      await carregar();
      sucessoToast('Evento SIPAT excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir evento SIPAT.');
    }
  }

  return (
    <div>
      {dialogElement}
      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Novo evento SIPAT</Text>
        </div>
        {erro && <Text className={estilos.erro}>{erro}</Text>}
        <div className={estilos.form}>
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
          <Field label="Ano de referência" required>
            <Input
              type="number"
              value={String(novo.anoReferencia)}
              onChange={(_, d) => setNovo({ ...novo, anoReferencia: Number(d.value) })}
            />
          </Field>
          <Field label="Início" required>
            <CampoData value={novo.dataInicio} onChange={(_, d) => setNovo({ ...novo, dataInicio: d.value })} />
          </Field>
          <Field label="Fim" required>
            <CampoData value={novo.dataFim} onChange={(_, d) => setNovo({ ...novo, dataFim: d.value })} />
          </Field>
          <Field label="Tema">
            <Input value={novo.tema ?? ''} onChange={(_, d) => setNovo({ ...novo, tema: d.value })} />
          </Field>
          <Field label="Programação">
            <Textarea value={novo.programacao ?? ''} onChange={(_, d) => setNovo({ ...novo, programacao: d.value })} />
          </Field>
        </div>
        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
            Criar evento
          </Button>
        </div>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Eventos SIPAT</Text>
        </div>
        {carregandoLista ? (
          <ListaCarregando />
        ) : lista.length === 0 ? (
          <EstadoVazio mensagem="Nenhum evento SIPAT cadastrado ainda." />
        ) : (
        <Table noNativeElements>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Obra</TableHeaderCell>
              <TableHeaderCell>Ano</TableHeaderCell>
              <TableHeaderCell>Período</TableHeaderCell>
              <TableHeaderCell>Tema</TableHeaderCell>
              <TableHeaderCell>Atividades</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {lista.map((e) => (
              <TableRow key={e.id} onClick={() => navigate(`/operacao/cipa/sipat/${e.id}`)} style={{ cursor: 'pointer' }}>
                <TableCell>{nomeObra(e.obraId)}</TableCell>
                <TableCell>{e.anoReferencia}</TableCell>
                <TableCell>
                  {e.dataInicio?.slice(0, 10)} a {e.dataFim?.slice(0, 10)}
                </TableCell>
                <TableCell>{e.tema ?? '—'}</TableCell>
                <TableCell>{e.totalAtividades}</TableCell>
                <TableCell>
                  <div style={{ display: 'flex', gap: 4 }}>
                    <Button
                      appearance="subtle"
                      icon={<ChevronRight24Regular />}
                      onClick={() => navigate(`/operacao/cipa/sipat/${e.id}`)}
                      aria-label="Ver evento"
                    />
                    <Button appearance="subtle" icon={<Delete24Regular />} onClick={(ev) => excluir(e.id, ev)} aria-label="Excluir" />
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
