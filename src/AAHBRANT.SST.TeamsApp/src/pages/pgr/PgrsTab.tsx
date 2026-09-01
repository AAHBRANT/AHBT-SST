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
} from '@fluentui/react-components';
import { Add24Regular, ChevronRight24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, statusPgrLabel, StatusPgr, type NovoPgr, type Obra, type Pgr } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

const pgrVazio: NovoPgr = {
  obraId: '',
  nome: '',
  descricao: '',
  dataElaboracao: '',
  dataProximaRevisao: null,
  responsavelUsuarioId: null,
  status: StatusPgr.EmElaboracao,
};

export function PgrsTab() {
  const estilos = usePageStyles();
  const navigate = useNavigate();
  const [pgrs, setPgrs] = useState<Pgr[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [novoPgr, setNovoPgr] = useState<NovoPgr>(pgrVazio);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [lista, obrs] = await Promise.all([api.pgrs.listar(), api.obras.listar()]);
      setPgrs(lista);
      setObras(obrs);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar PGRs.');
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
    try {
      setCarregando(true);
      setErro(null);
      await api.pgrs.criar({
        ...novoPgr,
        dataProximaRevisao: novoPgr.dataProximaRevisao || null,
      });
      setNovoPgr(pgrVazio);
      await carregar();
      sucessoToast('PGR criado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar PGR.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string, evento: React.MouseEvent) {
    evento.stopPropagation();
    if (!(await confirmar('Excluir este PGR? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.pgrs.excluir(id);
      await carregar();
      sucessoToast('PGR excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir PGR.');
    }
  }

  return (
    <div className={estilos.card}>
      {dialogElement}
      <div className={estilos.toolbar}>
        <Text weight="semibold">Programas de Gerenciamento de Riscos (PGR)</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Obra">
          <Select value={novoPgr.obraId} onChange={(_, d) => setNovoPgr({ ...novoPgr, obraId: d.value })}>
            <option value="">Selecione</option>
            {obras.map((obra) => (
              <option key={obra.id} value={obra.id}>
                {obra.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Nome do PGR">
          <Input value={novoPgr.nome} onChange={(_, d) => setNovoPgr({ ...novoPgr, nome: d.value })} />
        </Field>
        <Field label="Descrição">
          <Input
            value={novoPgr.descricao ?? ''}
            onChange={(_, d) => setNovoPgr({ ...novoPgr, descricao: d.value })}
          />
        </Field>
        <Field label="Data de elaboração">
          <Input
            type="date"
            value={novoPgr.dataElaboracao}
            onChange={(_, d) => setNovoPgr({ ...novoPgr, dataElaboracao: d.value })}
          />
        </Field>
        <Field label="Próxima revisão">
          <Input
            type="date"
            value={novoPgr.dataProximaRevisao ?? ''}
            onChange={(_, d) => setNovoPgr({ ...novoPgr, dataProximaRevisao: d.value || null })}
          />
        </Field>
        <Field label="Status">
          <Select
            value={novoPgr.status}
            onChange={(_, d) => setNovoPgr({ ...novoPgr, status: Number(d.value) })}
          >
            {Object.entries(statusPgrLabel).map(([valor, rotulo]) => (
              <option key={valor} value={valor}>
                {rotulo}
              </option>
            ))}
          </Select>
        </Field>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Adicionar PGR
        </Button>
      </div>

      {carregandoLista ? (
        <ListaCarregando />
      ) : pgrs.length === 0 ? (
        <EstadoVazio mensagem="Nenhum PGR cadastrado ainda." />
      ) : (
      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Nome</TableHeaderCell>
            <TableHeaderCell>Obra</TableHeaderCell>
            <TableHeaderCell>Elaboração</TableHeaderCell>
            <TableHeaderCell>Próxima revisão</TableHeaderCell>
            <TableHeaderCell>Status</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {pgrs.map((pgr) => (
            <TableRow key={pgr.id} onClick={() => navigate(`/prevencao/pgr/${pgr.id}`)} style={{ cursor: 'pointer' }}>
              <TableCell>{pgr.nome}</TableCell>
              <TableCell>{nomeObra(pgr.obraId)}</TableCell>
              <TableCell>{pgr.dataElaboracao?.slice(0, 10)}</TableCell>
              <TableCell>{pgr.dataProximaRevisao?.slice(0, 10)}</TableCell>
              <TableCell>{statusPgrLabel[pgr.status]}</TableCell>
              <TableCell>
                <div style={{ display: 'flex', gap: 4 }}>
                  <Button
                    appearance="subtle"
                    icon={<ChevronRight24Regular />}
                    onClick={() => navigate(`/prevencao/pgr/${pgr.id}`)}
                    aria-label="Ver PGR"
                  />
                  <Button
                    appearance="subtle"
                    icon={<Delete24Regular />}
                    onClick={(evento) => excluir(pgr.id, evento)}
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
