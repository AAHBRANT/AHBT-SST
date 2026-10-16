import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  BotaoAcao,
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
import { DICA_IA, EnvioPdfComIa, dataCampo, sugerirObra } from '../../components/leitura-ia/EnvioPdfComIa';

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
  // Preenchimento pelo PDF (16/10/2026): campos que vieram da IA ganham a dica "confira".
  const [arquivo, setArquivo] = useState<File | null>(null);
  const [lendoIa, setLendoIa] = useState(false);
  const [camposIa, setCamposIa] = useState<Set<keyof NovoPgr>>(new Set());
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

  async function lerPdf(f: File) {
    setArquivo(f);
    setCamposIa(new Set());
    try {
      setLendoIa(true);
      setErro(null);
      const c = await api.leiturasIa.cabecalhoPgr(f);
      const novo: Partial<NovoPgr> = {};
      const obraId = sugerirObra(`${c.obra ?? ''} ${c.nome ?? ''}`, obras);
      if (obraId) novo.obraId = obraId;
      if (c.nome) novo.nome = c.nome;
      const descricao = [c.revisao && `Revisão ${c.revisao}`, c.responsavel && `Resp. técnico: ${c.responsavel}${c.registro ? ` (${c.registro})` : ''}`]
        .filter(Boolean)
        .join(' — ');
      if (descricao) novo.descricao = descricao;
      if (c.dataElaboracao) novo.dataElaboracao = dataCampo(c.dataElaboracao);
      if (c.proximaRevisao) novo.dataProximaRevisao = dataCampo(c.proximaRevisao);
      if (c.termino) novo.dataTermino = dataCampo(c.termino);
      // Vigente quando hoje está dentro da vigência escrita no documento.
      const hoje = new Date().toISOString().slice(0, 10);
      if (c.dataElaboracao && dataCampo(c.dataElaboracao) <= hoje && (!c.termino || dataCampo(c.termino) >= hoje))
        novo.status = StatusPgr.Vigente;
      setNovoPgr((atual) => ({ ...atual, ...novo }));
      setCamposIa(new Set(Object.keys(novo) as (keyof NovoPgr)[]));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao ler o PDF com IA. Preencha os campos manualmente.');
    } finally {
      setLendoIa(false);
    }
  }

  const dica = (campo: keyof NovoPgr) => (camposIa.has(campo) ? DICA_IA : undefined);

  function limpar() {
    setNovoPgr(pgrVazio);
    setArquivo(null);
    setCamposIa(new Set());
  }

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      const { id } = await api.pgrs.criar({
        ...novoPgr,
        dataProximaRevisao: novoPgr.dataProximaRevisao || null,
        dataTermino: novoPgr.dataTermino || null,
      });
      if (arquivo) {
        // O PDF fica no PGR e já entra na fila da leitura completa (GHE, riscos, plano de ação).
        await api.pgrs.enviarDocumento(id, arquivo);
        await api.leiturasIa.iniciar('pgr', id).catch(() => undefined);
        limpar();
        sucessoToast('PGR criado. A IA está lendo o documento completo.');
        navigate(`/prevencao/pgr/${id}`);
        return;
      }
      limpar();
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
        <FormSection titulo="Enviar o PDF do PGR" numero={1} primeira>
          <EnvioPdfComIa arquivo={arquivo} lendo={lendoIa} lido={camposIa.size > 0} aoEscolher={lerPdf} aoRemover={() => setArquivo(null)} />
        </FormSection>
        <FormSection titulo="Dados do PGR" numero={2}>
          <FormGrid>
            <Campo span={3}>
              <Field label="Obra" hint={dica('obraId')}>
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
              <Field label="Nome do PGR" hint={dica('nome')}>
                <Input value={novoPgr.nome} onChange={(_, d) => setNovoPgr({ ...novoPgr, nome: d.value })} />
              </Field>
            </Campo>
            <Campo span={5}>
              <Field label="Descrição" hint={dica('descricao')}>
                <Input
                  value={novoPgr.descricao ?? ''}
                  onChange={(_, d) => setNovoPgr({ ...novoPgr, descricao: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Data de elaboração" hint={dica('dataElaboracao')}>
                <CampoData
                  value={novoPgr.dataElaboracao}
                  onChange={(_, d) => setNovoPgr({ ...novoPgr, dataElaboracao: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Próxima revisão" hint={dica('dataProximaRevisao')}>
                <CampoData
                  value={novoPgr.dataProximaRevisao ?? ''}
                  onChange={(_, d) => setNovoPgr({ ...novoPgr, dataProximaRevisao: d.value || null })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Término da vigência" hint={dica('dataTermino')}>
                <CampoData
                  value={novoPgr.dataTermino ?? ''}
                  onChange={(_, d) => setNovoPgr({ ...novoPgr, dataTermino: d.value || null })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Status" hint={dica('status')}>
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
            <Button onClick={limpar} disabled={carregando || lendoIa}>
              Limpar
            </Button>
            <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando || lendoIa}>
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
            <BotaoAcao tom="excluir" icon={<Delete24Regular />} onClick={() => excluir(p.id)} aria-label="Excluir" />
          )}
        />
      </Card>
      {dialogElement}
    </>
  );
}
