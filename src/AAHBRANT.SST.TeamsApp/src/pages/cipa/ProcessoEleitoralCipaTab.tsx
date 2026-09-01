import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Badge,
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
} from '@fluentui/react-components';
import { Add24Regular, ChevronRight24Regular, Delete24Regular } from '@fluentui/react-icons';
import {
  api,
  statusProcessoEleitoralCipaLabel,
  type NovoProcessoEleitoralCipa,
  type Obra,
  type ProcessoEleitoralCipa,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

function vazio(): NovoProcessoEleitoralCipa {
  return { obraId: '', numeroDocumento: '', dataConvocacao: '', dataInicioInscricoes: '', dataFimInscricoes: '', dataVotacao: '' };
}

export function ProcessoEleitoralCipaTab() {
  const estilos = usePageStyles();
  const navigate = useNavigate();
  const [lista, setLista] = useState<ProcessoEleitoralCipa[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [novo, setNovo] = useState<NovoProcessoEleitoralCipa>(vazio());
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [listaProcessos, listaObras] = await Promise.all([api.cipa.processosEleitorais.listar(), api.obras.listar()]);
      setLista(listaProcessos);
      setObras(listaObras);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar processos eleitorais.');
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
    if (!novo.obraId || !novo.dataConvocacao || !novo.dataInicioInscricoes || !novo.dataFimInscricoes || !novo.dataVotacao) {
      setErro('Preencha obra e todas as datas do processo.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.cipa.processosEleitorais.criar({ ...novo, numeroDocumento: novo.numeroDocumento || null });
      setNovo(vazio());
      await carregar();
      sucessoToast('Processo eleitoral criado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar processo eleitoral.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string, evento: React.MouseEvent) {
    evento.stopPropagation();
    if (!(await confirmar('Excluir este processo eleitoral? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.cipa.processosEleitorais.excluir(id);
      await carregar();
      sucessoToast('Processo eleitoral excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir processo eleitoral.');
    }
  }

  return (
    <div>
      {dialogElement}
      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Nova convocação de eleição</Text>
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
          <Field label="Nº do edital">
            <Input
              value={novo.numeroDocumento ?? ''}
              onChange={(_, d) => setNovo({ ...novo, numeroDocumento: d.value })}
            />
          </Field>
          <Field label="Data da convocação" required>
            <Input type="date" value={novo.dataConvocacao} onChange={(_, d) => setNovo({ ...novo, dataConvocacao: d.value })} />
          </Field>
          <Field label="Início das inscrições" required>
            <Input
              type="date"
              value={novo.dataInicioInscricoes}
              onChange={(_, d) => setNovo({ ...novo, dataInicioInscricoes: d.value })}
            />
          </Field>
          <Field label="Fim das inscrições" required>
            <Input
              type="date"
              value={novo.dataFimInscricoes}
              onChange={(_, d) => setNovo({ ...novo, dataFimInscricoes: d.value })}
            />
          </Field>
          <Field label="Data da votação" required>
            <Input type="date" value={novo.dataVotacao} onChange={(_, d) => setNovo({ ...novo, dataVotacao: d.value })} />
          </Field>
        </div>
        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
            Convocar eleição
          </Button>
        </div>
        <Text size={200} style={{ display: 'block', marginTop: 8 }}>
          Inscrição de candidatos, avaliação, apuração (manual, sem urna digital) e geração da ata em
          PDF são feitas na tela de detalhe do processo.
        </Text>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Processos eleitorais</Text>
        </div>
        {carregandoLista ? (
          <ListaCarregando />
        ) : lista.length === 0 ? (
          <EstadoVazio mensagem="Nenhum processo eleitoral cadastrado ainda." />
        ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Obra</TableHeaderCell>
              <TableHeaderCell>Nº edital</TableHeaderCell>
              <TableHeaderCell>Votação</TableHeaderCell>
              <TableHeaderCell>Candidatos</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {lista.map((p) => (
              <TableRow key={p.id} onClick={() => navigate(`/operacao/cipa/eleicao/${p.id}`)} style={{ cursor: 'pointer' }}>
                <TableCell>{nomeObra(p.obraId)}</TableCell>
                <TableCell>{p.numeroDocumento ?? '—'}</TableCell>
                <TableCell>{p.dataVotacao?.slice(0, 10)}</TableCell>
                <TableCell>{p.totalCandidatos}</TableCell>
                <TableCell>
                  <Badge appearance="tint">{statusProcessoEleitoralCipaLabel[p.status]}</Badge>
                </TableCell>
                <TableCell>
                  <div style={{ display: 'flex', gap: 4 }}>
                    <Button
                      appearance="subtle"
                      icon={<ChevronRight24Regular />}
                      onClick={() => navigate(`/operacao/cipa/eleicao/${p.id}`)}
                      aria-label="Ver processo"
                    />
                    <Button appearance="subtle" icon={<Delete24Regular />} onClick={(e) => excluir(p.id, e)} aria-label="Excluir" />
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
