import { useEffect, useState } from 'react';
import { Button, DataTable, Field, FeedbackInline, Input, Select, Text, useConfirmar, type Coluna } from '@ui';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import {
  api,
  TipoArea,
  tipoAreaLabel,
  statusAreaLabel,
  type AreaSst,
  type NovaAreaSst,
  type Obra,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useSucessoToast } from '../../hooks/useSucessoToast';

const areaVazia: NovaAreaSst = {
  codigo: '',
  nome: '',
  tipo: TipoArea.AreaDeTrabalho,
  obraId: '',
  detalhesLocalizacao: '',
  riscos: [],
  requisitos: [],
  status: 1,
};

// Onda 2 Task 9 (camada ui/): lista de Áreas de SST — Table→DataTable, erro→FeedbackInline,
// useConfirmarExclusao→useConfirmar (item 1, 4, 6 do Guia de conversão). O formulário de criação
// mantém o layout de estilos.card/formGrid de pageStyles (não tagueado pro item 2 nesta task).
export function AreasSstTab() {
  const estilos = usePageStyles();
  const [areas, setAreas] = useState<AreaSst[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [novaArea, setNovaArea] = useState<NovaAreaSst>(areaVazia);
  const [riscosTexto, setRiscosTexto] = useState('');
  const [requisitosTexto, setRequisitosTexto] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [ars, obrs] = await Promise.all([api.areasSst.listar(), api.obras.listar()]);
      setAreas(ars);
      setObras(obrs);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar áreas.');
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

  function dividirLista(texto: string): string[] {
    return texto
      .split(',')
      .map((item) => item.trim())
      .filter((item) => item.length > 0);
  }

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.areasSst.criar({
        ...novaArea,
        riscos: dividirLista(riscosTexto),
        requisitos: dividirLista(requisitosTexto),
      });
      setNovaArea(areaVazia);
      setRiscosTexto('');
      setRequisitosTexto('');
      await carregar();
      sucessoToast('Área criada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar área.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir esta área? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.areasSst.excluir(id);
      await carregar();
      sucessoToast('Área excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir área.');
    }
  }

  const colunas: Coluna<AreaSst>[] = [
    { chave: 'codigo', rotulo: 'Código' },
    { chave: 'nome', rotulo: 'Nome' },
    { chave: 'tipo', rotulo: 'Tipo', render: (a) => tipoAreaLabel[a.tipo] },
    { chave: 'obra', rotulo: 'Obra', render: (a) => nomeObra(a.obraId) },
    { chave: 'status', rotulo: 'Status', render: (a) => statusAreaLabel[a.status] },
    { chave: 'riscos', rotulo: 'Riscos', render: (a) => a.riscos.join(', ') },
  ];

  return (
    <div className={estilos.card}>
      {dialogElement}
      <div className={estilos.toolbar}>
        <Text weight="semibold">Áreas de SST cadastradas</Text>
      </div>

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>Dados da Área</div>
      <div className={estilos.formGrid}>
        <div className={estilos.col2}>
          <Field label="Código">
            <Input value={novaArea.codigo} onChange={(_, d) => setNovaArea({ ...novaArea, codigo: d.value })} />
          </Field>
        </div>
        <div className={estilos.col4}>
          <Field label="Nome">
            <Input value={novaArea.nome} onChange={(_, d) => setNovaArea({ ...novaArea, nome: d.value })} />
          </Field>
        </div>
        <div className={estilos.col3}>
          <Field label="Tipo">
            <Select
              value={String(novaArea.tipo)}
              onChange={(_, d) => setNovaArea({ ...novaArea, tipo: Number(d.value) })}
            >
              {Object.entries(tipoAreaLabel).map(([valor, rotulo]) => (
                <option key={valor} value={valor}>
                  {rotulo}
                </option>
              ))}
            </Select>
          </Field>
        </div>
        <div className={estilos.col3}>
          <Field label="Obra">
            <Select value={novaArea.obraId} onChange={(_, d) => setNovaArea({ ...novaArea, obraId: d.value })}>
              <option value="">Selecione</option>
              {obras.map((obra) => (
                <option key={obra.id} value={obra.id}>
                  {obra.nome}
                </option>
              ))}
            </Select>
          </Field>
        </div>
        <div className={estilos.col4}>
          <Field label="Detalhes da localização">
            <Input
              value={novaArea.detalhesLocalizacao ?? ''}
              onChange={(_, d) => setNovaArea({ ...novaArea, detalhesLocalizacao: d.value })}
            />
          </Field>
        </div>
        <div className={estilos.col4}>
          <Field label="Riscos (separados por vírgula)">
            <Input value={riscosTexto} onChange={(_, d) => setRiscosTexto(d.value)} />
          </Field>
        </div>
        <div className={estilos.col4}>
          <Field label="Requisitos (separados por vírgula)">
            <Input value={requisitosTexto} onChange={(_, d) => setRequisitosTexto(d.value)} />
          </Field>
        </div>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Adicionar área
        </Button>
      </div>

      <DataTable
        aria-label="Áreas de SST cadastradas"
        colunas={colunas}
        linhas={areas}
        chaveLinha={(a) => a.id}
        carregando={carregandoLista}
        vazio={{ titulo: 'Nenhuma área cadastrada ainda.' }}
        acoesLinha={(a) => (
          <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(a.id)} aria-label="Excluir" />
        )}
      />
    </div>
  );
}
