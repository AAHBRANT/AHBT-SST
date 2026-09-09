import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button,
  Campo,
  Card,
  CampoData,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  Select,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, statusPgrLabel, StatusPgr, type NovoPgr, type Obra, type Pgr } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

const pgrVazio: NovoPgr = {
  obraId: '',
  nome: '',
  descricao: '',
  dataElaboracao: '',
  dataProximaRevisao: null,
  dataTermino: null,
  responsavelUsuarioId: null,
  status: StatusPgr.EmElaboracao,
};

// Onda 2 Task 8 (camada ui/): lista + formulário de criação de PGR. É o exemplo literal do Guia de
// conversão (item 1, Table→DataTable) — aplicado aqui junto com os itens 2/4/6 (Card+FormSection no
// lugar de estilos.card/toolbar, FeedbackInline no lugar de estilos.erro, useConfirmar no lugar de
// useConfirmarExclusao), mesmo formato de AprsTab.tsx (Task 11): formulário em FormSection dentro do
// mesmo Card da tabela, sem PainelLateral (a lista já é curta o bastante para não precisar).
export function PgrsTab() {
  const navigate = useNavigate();
  const [pgrs, setPgrs] = useState<Pgr[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [novoPgr, setNovoPgr] = useState<NovoPgr>(pgrVazio);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmar();
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
        dataTermino: novoPgr.dataTermino || null,
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

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este PGR? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.pgrs.excluir(id);
      await carregar();
      sucessoToast('PGR excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir PGR.');
    }
  }

  const colunas: Coluna<Pgr>[] = [
    { chave: 'nome', rotulo: 'Nome' },
    { chave: 'obra', rotulo: 'Obra', render: (p) => nomeObra(p.obraId) },
    { chave: 'elaboracao', rotulo: 'Elaboração', render: (p) => p.dataElaboracao?.slice(0, 10) ?? '' },
    { chave: 'revisao', rotulo: 'Próxima revisão', render: (p) => p.dataProximaRevisao?.slice(0, 10) ?? '' },
    { chave: 'termino', rotulo: 'Término', render: (p) => p.dataTermino?.slice(0, 10) ?? '' },
    { chave: 'status', rotulo: 'Status', render: (p) => statusPgrLabel[p.status] },
  ];

  return (
    <>
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <Card titulo="Programas de Gerenciamento de Riscos (PGR)">
        <FormSection titulo="Dados do PGR" numero={1} primeira>
          <FormGrid>
            <Campo span={3}>
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
            </Campo>
            <Campo span={4}>
              <Field label="Nome do PGR">
                <Input value={novoPgr.nome} onChange={(_, d) => setNovoPgr({ ...novoPgr, nome: d.value })} />
              </Field>
            </Campo>
            <Campo span={5}>
              <Field label="Descrição">
                <Input
                  value={novoPgr.descricao ?? ''}
                  onChange={(_, d) => setNovoPgr({ ...novoPgr, descricao: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Data de elaboração">
                <CampoData
                  value={novoPgr.dataElaboracao}
                  onChange={(_, d) => setNovoPgr({ ...novoPgr, dataElaboracao: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Próxima revisão">
                <CampoData
                  value={novoPgr.dataProximaRevisao ?? ''}
                  onChange={(_, d) => setNovoPgr({ ...novoPgr, dataProximaRevisao: d.value || null })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Término da vigência">
                <CampoData
                  value={novoPgr.dataTermino ?? ''}
                  onChange={(_, d) => setNovoPgr({ ...novoPgr, dataTermino: d.value || null })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
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
            </Campo>
          </FormGrid>
          <FormRodape>
            <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
              Adicionar PGR
            </Button>
          </FormRodape>
        </FormSection>

        <DataTable
          aria-label="PGRs cadastrados"
          colunas={colunas}
          linhas={pgrs}
          chaveLinha={(p) => p.id}
          carregando={carregandoLista}
          vazio={{ titulo: 'Nenhum PGR cadastrado ainda.' }}
          aoClicarLinha={(p) => navigate(`/prevencao/pgr/${p.id}`)}
          acoesLinha={(p) => (
            <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(p.id)} aria-label="Excluir" />
          )}
        />
      </Card>
      {dialogElement}
    </>
  );
}
