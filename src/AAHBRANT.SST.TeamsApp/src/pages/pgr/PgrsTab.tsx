import { useEffect, useMemo, useState } from 'react';
import { gravar, lerLembrada } from '../../hooks/useObraLembrada';
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
  StatusChip,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular, DocumentPdf24Regular } from '@fluentui/react-icons';
import {
  api,
  statusPgrLabel,
  StatusObra,
  StatusPgr,
  type NovoPgr,
  type Obra,
  type Pgr,
  type Atividade,
  type ResumoGeracaoPgrEap,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { DICA_IA, EnvioPdfComIa, dataCampo, sugerirObra } from '../../components/leitura-ia/EnvioPdfComIa';
import { CartoesVigentesPorObra, dataBr, type CartaoDocumentoObra } from '../../components/documentos-obra/CartoesVigentesPorObra';
import { BarraObraAcoes } from '../../components/documentos-obra/BarraObraAcoes';
import { useVisualizadorPdf } from '../../components/useVisualizadorPdf';
import { useSouAdministrador } from '../../lib/UsuarioLogadoContext';
import { rotuloStatusGeracao } from './GeracoesPgrEapCard';

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

// Linha da lista única: PGR cadastrado (importado ou vindo da EAP) ou geração pela EAP ainda não aprovada.
type LinhaPgr =
  | { tipo: 'pgr'; id: string; pgr: Pgr; geracao?: ResumoGeracaoPgrEap }
  | { tipo: 'geracao'; id: string; geracao: ResumoGeracaoPgrEap };

// Tela PGRs enxuta (06/11/2026, mockup pgr-pcmso-geral_2026-11-06_v1): filtro por obra, PGR vigente de
// cada obra no topo, formulário de PDF só quando pedido, e uma lista única que junta os PGRs
// cadastrados e as gerações pela EAP em andamento (antes eram dois blocos separados).
export function PgrsTab() {
  const navigate = useNavigate();
  const [pgrs, setPgrs] = useState<Pgr[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [geracoes, setGeracoes] = useState<ResumoGeracaoPgrEap[]>([]);
  // Obra lembrada entre as telas da Gestão de SST (08/11/2026).
  const [obraFiltro, setObraFiltroBase] = useState(() => lerLembrada());
  const setObraFiltro = (id: string) => {
    setObraFiltroBase(id);
    gravar(id);
  };
  const [mostrarForm, setMostrarForm] = useState(false);
  const [novoPgr, setNovoPgr] = useState<NovoPgr>(pgrVazio);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  // Preenchimento pelo PDF (16/10/2026): campos que vieram da IA ganham a dica "confira".
  const [arquivo, setArquivo] = useState<File | null>(null);
  const [lendoIa, setLendoIa] = useState(false);
  const [camposIa, setCamposIa] = useState<Set<keyof NovoPgr>>(new Set());
  const { confirmar, dialogElement } = useConfirmar();
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();
  const souAdministrador = useSouAdministrador();
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [lista, obrs, gers] = await Promise.all([
        api.pgrs.listar(),
        api.obras.listar(),
        api.geracoesPgrEap.listar().catch(() => [] as ResumoGeracaoPgrEap[]),
      ]);
      api.atividades.listar().then(setAtividades).catch(() => undefined);
      setPgrs(lista);
      setObras(obrs);
      setGeracoes(gers);
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

  function cancelarForm() {
    limpar();
    setMostrarForm(false);
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
      cancelarForm();
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

  // Rascunho = geração pela EAP que nunca foi para aprovação (situação 1). Só Administrador exclui.
  async function excluirRascunho(g: ResumoGeracaoPgrEap) {
    const ok = await confirmar({
      titulo: 'Excluir este rascunho?',
      mensagem: `${g.nome} · ${g.obraNome}. Os GHEs, riscos e medidas sugeridos pela IA neste rascunho serão perdidos. Isso não altera nenhum PGR vigente.`,
      rotuloConfirmar: 'Excluir rascunho',
    });
    if (!ok) return;
    try {
      await api.geracoesPgrEap.excluir(g.id);
      await carregar();
      sucessoToast(`Rascunho excluído: ${g.nome}`);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível excluir o rascunho.');
    }
  }

  function abrirPdf(pgr: Pgr, geracao?: ResumoGeracaoPgrEap) {
    void visualizar({
      titulo: pgr.nome,
      nomeArquivo: `${pgr.nome}.pdf`,
      // PGR da EAP: PDF gerado pelo sistema. Importado: o PDF da consultoria anexado no cadastro.
      obter: async () => {
        if (geracao) return api.geracoesPgrEap.baixarPdf(geracao.id);
        const blob = await api.pgrs.obterDocumento(pgr.id);
        if (!blob) throw new Error('Este PGR não tem PDF anexado.');
        return blob;
      },
    });
  }

  const cartoes = useMemo<CartaoDocumentoObra[]>(() => {
    const geracaoDoPgr = (id: string) => geracoes.find((g) => g.pgrId === id);
    const obraOk = (obraId: string) => !obraFiltro || obraId === obraFiltro;
    const nome = (id: string) => obras.find((o) => o.id === id)?.nome ?? '';

    const vigentes: CartaoDocumentoObra[] = pgrs
      .filter((p) => p.status === StatusPgr.Vigente && obraOk(p.obraId))
      .map((p) => {
        const g = geracaoDoPgr(p.id);
        return {
          chave: p.id,
          titulo: nome(p.obraId),
          obra: p.nome,
          tags: [
            { rotulo: 'Vigente', tom: 'ok' as const },
            g ? { rotulo: 'Gerado pela EAP', tom: 'atencao' as const } : { rotulo: 'Importado (legado)', tom: 'neutro' as const },
          ],
          linhas: g?.aprovadoPorNome ? [{ rotulo: 'Aprovado por', valor: g.aprovadoPorNome }] : [],
          vigencia: { inicio: p.dataElaboracao, fim: p.dataProximaRevisao ?? p.dataTermino },
          dica: g ? undefined : 'Na próxima revisão, gere pelo sistema (EAP).',
          numeros: (() => {
            const ats = atividades.filter((a) => a.obraId === p.obraId);
            return [
              { rotulo: 'atividades', valor: ats.length },
              { rotulo: 'riscos altos', valor: ats.reduce((t, a) => t + (a.riscosAltos ?? 0), 0) },
            ];
          })(),
          acao: { rotulo: 'Abrir PDF', aoClicar: () => abrirPdf(p, g) },
          acaoExtra: { rotulo: 'Ver PGR completo →', aoClicar: () => navigate(`/prevencao/pgr/${p.id}`) },
        };
      })
      .sort((a, b) => a.obra.localeCompare(b.obra, 'pt-BR'));

    // Obra em andamento sem PGR vigente: cartão laranja com o próximo passo.
    const comVigente = new Set(pgrs.filter((p) => p.status === StatusPgr.Vigente).map((p) => p.obraId));
    const pendentes: CartaoDocumentoObra[] = obras
      .filter((o) => o.status === StatusObra.EmAndamento && !comVigente.has(o.id) && obraOk(o.id))
      .map((o) => {
        const g = geracoes.find((x) => x.obraNome === o.nome && x.status !== 3);
        return {
          chave: `sem-${o.id}`,
          titulo: 'Sem PGR vigente',
          obra: o.nome,
          pendente: true,
          tags: [{ rotulo: 'Pendente', tom: 'atencao' as const }],
          linhas: [
            {
              rotulo: 'Situação',
              valor: g ? `Geração pela EAP: ${rotuloStatusGeracao[g.status].rotulo.toLowerCase()}` : 'Nenhum PGR cadastrado',
            },
          ],
          acao: g
            ? { rotulo: 'Ver geração', secundaria: true, aoClicar: () => navigate(`/prevencao/pgr/gerar-eap/${g.id}`) }
            : { rotulo: 'Gerar pela EAP', secundaria: true, aoClicar: () => navigate('/prevencao/pgr/gerar-eap') },
        };
      })
      .sort((a, b) => a.obra.localeCompare(b.obra, 'pt-BR'));

    return [...vigentes, ...pendentes];
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pgrs, obras, geracoes, obraFiltro, atividades]);

  const linhas = useMemo<LinhaPgr[]>(() => {
    const obraIdPorNome = (nome: string) => obras.find((o) => o.nome === nome)?.id ?? '';
    const obraOk = (obraId: string) => !obraFiltro || obraId === obraFiltro;
    const emAndamento: LinhaPgr[] = geracoes
      .filter((g) => g.status !== 3 && obraOk(obraIdPorNome(g.obraNome)))
      .map((g) => ({ tipo: 'geracao', id: `g-${g.id}`, geracao: g }));
    // O vigente já está no cartão de cima; aqui só em elaboração, em revisão e encerrados.
    const cadastrados: LinhaPgr[] = pgrs
      .filter((p) => p.status !== StatusPgr.Vigente && obraOk(p.obraId))
      .map((p) => ({ tipo: 'pgr', id: p.id, pgr: p, geracao: geracoes.find((g) => g.pgrId === p.id) }));
    return [...emAndamento, ...cadastrados];
  }, [pgrs, obras, geracoes, obraFiltro]);

  const colunas: Coluna<LinhaPgr>[] = [
    { chave: 'nome', rotulo: 'PGR', render: (l) => (l.tipo === 'pgr' ? l.pgr.nome : l.geracao.nome) },
    { chave: 'obra', rotulo: 'Obra', render: (l) => (l.tipo === 'pgr' ? nomeObra(l.pgr.obraId) : l.geracao.obraNome) },
    { chave: 'origem', rotulo: 'Origem', render: (l) => (l.geracao ? 'EAP' : 'Importado (legado)') },
    {
      chave: 'situacao',
      rotulo: 'Situação',
      render: (l) =>
        l.tipo === 'pgr' ? (
          statusPgrLabel[l.pgr.status]
        ) : (
          <StatusChip tom={rotuloStatusGeracao[l.geracao.status].tom}>
            {`${rotuloStatusGeracao[l.geracao.status].rotulo} · passo ${l.geracao.passo} de 5`}
          </StatusChip>
        ),
    },
    { chave: 'elaboracao', rotulo: 'Elaboração', render: (l) => (l.tipo === 'pgr' ? dataBr(l.pgr.dataElaboracao) : '—') },
    {
      chave: 'revisao',
      rotulo: 'Próxima revisão',
      render: (l) => (l.tipo === 'pgr' ? dataBr(l.pgr.dataProximaRevisao) : '—'),
    },
  ];

  return (
    <>
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <BarraObraAcoes obras={obras} obraId={obraFiltro} aoMudarObra={setObraFiltro}>
            {/* Todo PGR nasce da EAP (06/11/2026). Importar PDF fica só para Admin, para PGR antigo. */}
            {souAdministrador && (
              <Button appearance="subtle" icon={<DocumentPdf24Regular />} onClick={() => setMostrarForm(true)} disabled={mostrarForm}>
                Importar PGR antigo (Admin)
              </Button>
            )}
            <Button appearance="primary" icon={<Add24Regular />} onClick={() => navigate('/prevencao/pgr/gerar-eap')}>
              Gerar PGR pela EAP
            </Button>
      </BarraObraAcoes>

      {mostrarForm && (
        <Card titulo="Importar PGR antigo (PDF)" subtitulo="Só para PGR feito antes do gerador pela EAP. PGR novo deve ser gerado no sistema.">
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
              <Button onClick={cancelarForm} disabled={carregando || lendoIa}>
                Cancelar
              </Button>
              <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando || lendoIa}>
                Salvar PGR
              </Button>
            </FormRodape>
          </FormSection>
        </Card>
      )}

      <CartoesVigentesPorObra
        titulo="PGR vigente por obra"
        subtitulo="Importados e gerados no sistema pela EAP"
        cartoes={cartoes}
        carregando={carregandoLista}
        vazio="Nenhum PGR vigente nem obra em andamento para esta seleção."
      />

      {linhas.length > 0 && (
      <Card titulo="Outros PGRs" subtitulo="Em elaboração, gerações pela EAP em andamento e histórico">
        <DataTable
          aria-label="Outros PGRs"
          colunas={colunas}
          linhas={linhas}
          chaveLinha={(l) => l.id}
          carregando={carregandoLista}
          vazio={{ titulo: 'Nenhum PGR cadastrado ainda.' }}
          aoClicarLinha={(l) =>
            navigate(l.tipo === 'pgr' ? `/prevencao/pgr/${l.pgr.id}` : `/prevencao/pgr/gerar-eap/${l.geracao.id}`)
          }
          acoesLinha={(l) =>
            l.tipo === 'pgr' ? (
              <BotaoAcao tom="excluir" icon={<Delete24Regular />} onClick={() => excluir(l.pgr.id)} aria-label="Excluir" />
            ) : souAdministrador && l.geracao.status === 1 ? (
              <BotaoAcao
                tom="excluir"
                icon={<Delete24Regular />}
                onClick={() => excluirRascunho(l.geracao)}
                aria-label={`Excluir rascunho ${l.geracao.nome}`}
              />
            ) : null
          }
        />
      </Card>
      )}
      {dialogElement}
      {dialogoVisualizador}
    </>
  );
}
