import { useEffect, useState } from 'react';
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
  Textarea,
} from '@fluentui/react-components';
import { Add24Regular, Delete24Regular, Edit24Regular } from '@fluentui/react-icons';
import {
  api,
  TipoAtivo,
  tipoAtivoLabel,
  type AtivoSst,
  type NovoAtivoSst,
  type Obra,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

function ativoVazio(): NovoAtivoSst {
  return {
    obraId: '',
    tipoAtivo: TipoAtivo.Extintor,
    identificacao: '',
    descricao: '',
    localizacao: '',
    dataValidade: '',
    observacoes: '',
  };
}

// A validade aqui é um campo fixo cadastrado (DataValidade) — não calculado a partir de um
// histórico de registros — então o cálculo do badge é só a diferença
// entre a data cadastrada e hoje.
function vencimentoInfo(dataValidade: string): { texto: string; cor: 'success' | 'warning' | 'danger' } {
  const hoje = new Date();
  const vencimento = new Date(dataValidade);
  const diffDias = Math.ceil((vencimento.getTime() - hoje.getTime()) / (1000 * 60 * 60 * 24));
  const texto = dataValidade.slice(0, 10);
  if (diffDias < 0) return { texto: `${texto} (vencido)`, cor: 'danger' };
  if (diffDias <= 30) return { texto: `${texto} (vence em ${diffDias}d)`, cor: 'warning' };
  return { texto, cor: 'success' };
}

// Cadastro de Ativos de SST (Motor Central de Alertas + Cadastro de Ativos, requisito do usuário
// em 2026-08-25): extintores e equipamentos monitorados como uma única entidade AtivoSst,
// discriminada por TipoAtivo. Fica no pilar Operação, ao lado de Identificação (NTAG/QR).
export function AtivosPage() {
  const estilos = usePageStyles();
  const [ativos, setAtivos] = useState<AtivoSst[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [form, setForm] = useState<NovoAtivoSst>(ativoVazio());
  const [editandoId, setEditandoId] = useState<string | null>(null);
  const [filtroTipo, setFiltroTipo] = useState<string>('');
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [lista, listaObras] = await Promise.all([api.ativos.listar(), api.obras.listar()]);
      setAtivos(lista);
      setObras(listaObras);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar ativos.');
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

  function iniciarEdicao(ativo: AtivoSst) {
    setEditandoId(ativo.id);
    setForm({
      obraId: ativo.obraId,
      tipoAtivo: ativo.tipoAtivo,
      identificacao: ativo.identificacao,
      descricao: ativo.descricao,
      localizacao: ativo.localizacao ?? '',
      dataValidade: ativo.dataValidade.slice(0, 10),
      observacoes: ativo.observacoes ?? '',
    });
  }

  function cancelarEdicao() {
    setEditandoId(null);
    setForm(ativoVazio());
  }

  async function salvar() {
    if (!form.obraId || !form.identificacao || !form.descricao || !form.dataValidade) {
      setErro('Preencha obra, identificação, descrição e data de validade.');
      return;
    }
    const eraEdicao = !!editandoId;
    try {
      setCarregando(true);
      setErro(null);
      if (editandoId) {
        await api.ativos.atualizar(editandoId, { id: editandoId, obraNome: '', ...form });
      } else {
        await api.ativos.criar(form);
      }
      cancelarEdicao();
      await carregar();
      sucessoToast(eraEdicao ? 'Ativo atualizado com sucesso.' : 'Ativo cadastrado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar ativo.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este ativo? Essa ação não pode ser desfeita.'))) return;
    try {
      setErro(null);
      await api.ativos.excluir(id);
      if (editandoId === id) cancelarEdicao();
      await carregar();
      sucessoToast('Ativo excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir ativo.');
    }
  }

  const ativosFiltrados = filtroTipo ? ativos.filter((a) => String(a.tipoAtivo) === filtroTipo) : ativos;

  return (
    <div>
      {dialogElement}
      <div style={{ marginBottom: 16 }}>
        <Text size={500} weight="semibold">
          Ativos de SST (Extintores &amp; Equipamentos)
        </Text>
      </div>

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">{editandoId ? 'Editar ativo' : 'Novo ativo'}</Text>
        </div>

        {erro && <Text className={estilos.erro}>{erro}</Text>}

        <div className={estilos.form}>
          <Field label="Obra">
            <Select value={form.obraId} onChange={(_, d) => setForm({ ...form, obraId: d.value })}>
              <option value="">Selecione</option>
              {obras.map((obra) => (
                <option key={obra.id} value={obra.id}>
                  {obra.nome}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Tipo">
            <Select
              value={String(form.tipoAtivo)}
              onChange={(_, d) => setForm({ ...form, tipoAtivo: Number(d.value) })}
            >
              {Object.entries(tipoAtivoLabel).map(([valor, rotulo]) => (
                <option key={valor} value={valor}>
                  {rotulo}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Identificação (nº de série/etiqueta)">
            <Input
              value={form.identificacao}
              onChange={(_, d) => setForm({ ...form, identificacao: d.value })}
            />
          </Field>
          <Field label="Descrição (nome/modelo)">
            <Input value={form.descricao} onChange={(_, d) => setForm({ ...form, descricao: d.value })} />
          </Field>
          <Field label="Localização (opcional)">
            <Input
              value={form.localizacao ?? ''}
              onChange={(_, d) => setForm({ ...form, localizacao: d.value })}
            />
          </Field>
          <Field label="Data de validade">
            <Input
              type="date"
              value={form.dataValidade}
              onChange={(_, d) => setForm({ ...form, dataValidade: d.value })}
            />
          </Field>
          <Field label="Observações (opcional)">
            <Textarea
              value={form.observacoes ?? ''}
              onChange={(_, d) => setForm({ ...form, observacoes: d.value })}
            />
          </Field>
        </div>

        <div className={estilos.formActions}>
          {editandoId && (
            <Button appearance="secondary" onClick={cancelarEdicao} disabled={carregando}>
              Cancelar edição
            </Button>
          )}
          <Button
            appearance="primary"
            icon={editandoId ? undefined : <Add24Regular />}
            onClick={salvar}
            disabled={carregando}
          >
            {editandoId ? 'Salvar alterações' : 'Cadastrar ativo'}
          </Button>
        </div>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Ativos cadastrados</Text>
          <Field label="Filtrar por tipo">
            <Select value={filtroTipo} onChange={(_, d) => setFiltroTipo(d.value)}>
              <option value="">Todos</option>
              {Object.entries(tipoAtivoLabel).map(([valor, rotulo]) => (
                <option key={valor} value={valor}>
                  {rotulo}
                </option>
              ))}
            </Select>
          </Field>
        </div>

        {carregandoLista ? (
          <ListaCarregando />
        ) : ativosFiltrados.length === 0 ? (
          <EstadoVazio mensagem="Nenhum ativo encontrado." />
        ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Obra</TableHeaderCell>
              <TableHeaderCell>Tipo</TableHeaderCell>
              <TableHeaderCell>Identificação</TableHeaderCell>
              <TableHeaderCell>Descrição</TableHeaderCell>
              <TableHeaderCell>Localização</TableHeaderCell>
              <TableHeaderCell>Validade</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {ativosFiltrados.map((ativo) => {
              const vencimento = vencimentoInfo(ativo.dataValidade);
              return (
                <TableRow key={ativo.id}>
                  <TableCell>{ativo.obraNome || nomeObra(ativo.obraId)}</TableCell>
                  <TableCell>{tipoAtivoLabel[ativo.tipoAtivo]}</TableCell>
                  <TableCell>{ativo.identificacao}</TableCell>
                  <TableCell>{ativo.descricao}</TableCell>
                  <TableCell>{ativo.localizacao}</TableCell>
                  <TableCell>
                    <Badge color={vencimento.cor} appearance="tint">
                      {vencimento.texto}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <Button
                      appearance="subtle"
                      icon={<Edit24Regular />}
                      onClick={() => iniciarEdicao(ativo)}
                      aria-label="Editar"
                    />
                    <Button
                      appearance="subtle"
                      icon={<Delete24Regular />}
                      onClick={() => excluir(ativo.id)}
                      aria-label="Excluir"
                    />
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
        )}
      </div>
    </div>
  );
}
