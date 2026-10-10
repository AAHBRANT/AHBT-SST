import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Button,
  Card,
  Carregando,
  DataTable,
  FeedbackInline,
  KpiCard,
  PageHeader,
  Select,
  StatusChip,
  Text,
  tokensUi,
  useConfirmar,
  type Coluna,
  type Tom,
} from '@ui';
import {
  api,
  StatusLeituraIa,
  type DocumentoLeituraIa,
  type Funcao,
  type FuncaoLidaIa,
  type GheLidoIa,
  type LeituraIa,
  type ResultadoLeituraIa,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { nomeExame, rotuloPeriodicidade } from '../../components/estrutura-sst/formatacao';

const CRIAR = '__criar__';

const tomSituacao: Record<FuncaoLidaIa['situacao'], Tom> = { existe: 'ok', parecida: 'atencao', nova: 'info' };
const rotuloSituacao: Record<FuncaoLidaIa['situacao'], string> = { existe: 'já existe', parecida: 'confirme', nova: 'nova' };
const tomDiferenca: Record<string, Tom> = { incluido: 'info', removido: 'alerta', alterado: 'atencao' };
const rotuloDiferenca: Record<string, string> = { incluido: 'entra', removido: 'sai', alterado: 'muda' };

// Revisão da leitura com IA (aprovada no mockup de 10/10/2026): nada é gravado na estrutura da obra
// antes do clique em "Cadastrar estrutura".
export function LeituraIaRevisaoPage() {
  const { documento, id } = useParams<{ documento: DocumentoLeituraIa; id: string }>();
  const navigate = useNavigate();
  const sucessoToast = useSucessoToast();
  const { confirmar, dialogElement } = useConfirmar();
  const [leitura, setLeitura] = useState<LeituraIa | null>(null);
  const [funcoesSistema, setFuncoesSistema] = useState<Funcao[]>([]);
  const [escolhas, setEscolhas] = useState<Record<string, string>>({});
  const [erro, setErro] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);
  const [gheAberto, setGheAberto] = useState<number | null>(null);

  const doc: DocumentoLeituraIa = documento === 'pcmso' ? 'pcmso' : 'pgr';
  const nome = doc === 'pgr' ? 'PGR' : 'PCMSO';

  useEffect(() => {
    if (!id) return;
    Promise.all([api.leiturasIa.obter(doc, id), api.funcoes.listar()])
      .then(([l, funcoes]) => {
        setLeitura(l);
        setFuncoesSistema([...funcoes].sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR')));
        const inicial: Record<string, string> = {};
        l.resultado?.funcoes.forEach((f) => (inicial[f.nomeDocumento] = f.funcaoIdSugerida ?? CRIAR));
        setEscolhas(inicial);
      })
      .catch((e: unknown) => setErro(e instanceof Error ? e.message : 'Falha ao carregar a leitura.'));
  }, [doc, id]);

  const resultado = leitura?.resultado ?? null;
  const caminhoDocumento = leitura
    ? doc === 'pgr'
      ? `/prevencao/pgr/${leitura.documentoId}`
      : `/operacao/saude-ocupacional/pcmso/${leitura.documentoId}`
    : undefined;
  const voltar = () => {
    if (caminhoDocumento) navigate(caminhoDocumento);
  };

  async function cadastrar() {
    if (!leitura || !resultado) return;
    const pendentesConfirmacao = resultado.funcoes.filter((f) => f.situacao === 'parecida').length;
    const texto = resultado.haEstruturaAtual
      ? `Cadastrar esta leitura substitui a estrutura atual do ${nome} (a anterior fica guardada, sem uso).`
      : `Cadastrar a estrutura lida do ${nome} na obra?`;
    const aviso = pendentesConfirmacao > 0 ? ` Confira as ${pendentesConfirmacao} funções marcadas "confirme" antes.` : '';
    if (!(await confirmar(texto + aviso))) return;
    try {
      setSalvando(true);
      setErro(null);
      const r = await api.leiturasIa.cadastrar(
        doc,
        leitura.id,
        resultado.funcoes.map((f) => ({
          nomeDocumento: f.nomeDocumento,
          funcaoId: escolhas[f.nomeDocumento] && escolhas[f.nomeDocumento] !== CRIAR ? escolhas[f.nomeDocumento] : null,
        })),
      );
      sucessoToast(
        doc === 'pgr'
          ? `Estrutura cadastrada: ${r.ghes} GHE, ${r.riscos} riscos e ${r.itensPlanoAcao} itens no plano de ação.`
          : `Exames cadastrados: ${r.exames} exames por função e ${r.itensPlanoAcao} pendências no plano do PCMSO.`,
      );
      voltar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao cadastrar a estrutura.');
    } finally {
      setSalvando(false);
    }
  }

  async function descartar() {
    if (!leitura || !(await confirmar('Descartar esta leitura? Nada foi cadastrado e você pode ler de novo depois.'))) return;
    try {
      await api.leiturasIa.descartar(doc, leitura.id);
      voltar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao descartar a leitura.');
    }
  }

  if (erro && !leitura) return <FeedbackInline tom="erro">{erro}</FeedbackInline>;
  if (!leitura) return <Carregando />;

  const podeCadastrar = leitura.status === StatusLeituraIa.Concluida && !!resultado;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <PageHeader titulo={`Revisão da leitura com IA · ${nome}`} voltarPara={caminhoDocumento} rotuloVoltar={`Voltar para o ${nome}`} />
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      {leitura.status === StatusLeituraIa.Cadastrada && (
        <FeedbackInline tom="sucesso">Esta leitura já foi cadastrada.</FeedbackInline>
      )}
      {leitura.status === StatusLeituraIa.Descartada && <FeedbackInline tom="aviso">Esta leitura foi descartada.</FeedbackInline>}
      {!resultado ? (
        <FeedbackInline tom="info">A leitura ainda não terminou.</FeedbackInline>
      ) : (
        <>
          <Resumo resultado={resultado} />
          {resultado.haEstruturaAtual && <Diferencas resultado={resultado} />}
          <TabelaFuncoes
            resultado={resultado}
            funcoesSistema={funcoesSistema}
            escolhas={escolhas}
            aoEscolher={(nomeDoc, valor) => setEscolhas((e) => ({ ...e, [nomeDoc]: valor }))}
            editavel={podeCadastrar}
          />
          <Divergencias resultado={resultado} />
          {doc === 'pgr' ? (
            <RiscosPorGhe ghes={resultado.ghes} aberto={gheAberto} aoAbrir={setGheAberto} />
          ) : (
            <ExamesLidos resultado={resultado} />
          )}
          {podeCadastrar && (
            <Card>
              <div style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap', justifyContent: 'flex-end' }}>
                <Text size={200} style={{ marginRight: 'auto', maxWidth: '60ch' }}>
                  Os valores entram como estão no documento. As divergências viram pendências de validação técnica no plano de
                  ação. Classificação e exames continuam sujeitos à validação do engenheiro de segurança e da médica coordenadora.
                </Text>
                <Button appearance="secondary" onClick={descartar} disabled={salvando}>
                  Descartar leitura
                </Button>
                <Button appearance="primary" onClick={cadastrar} disabled={salvando}>
                  {salvando ? 'Cadastrando…' : 'Cadastrar estrutura'}
                </Button>
              </div>
            </Card>
          )}
        </>
      )}
      {dialogElement}
    </div>
  );
}

function Resumo({ resultado }: { resultado: ResultadoLeituraIa }) {
  const funcoes = resultado.funcoes;
  const existentes = funcoes.filter((f) => f.situacao === 'existe').length;
  const riscos = resultado.ghes.reduce((s, g) => s + g.riscos.filter((r) => r.probabilidade > 0 && r.severidade > 0).length, 0);
  const exames = resultado.examesPorFuncao.reduce((s, f) => s + f.exames.length, 0);
  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: 12 }}>
      {resultado.documento === 'PGR' ? (
        <>
          <KpiCard tom="info" rotulo="GHE" valor={resultado.ghes.length} />
          <KpiCard tom="info" rotulo="Riscos" valor={riscos} />
          <KpiCard tom="info" rotulo="Ações do plano" valor={resultado.planoAcao.length} />
        </>
      ) : (
        <KpiCard tom="info" rotulo="Exames por função" valor={exames} />
      )}
      <KpiCard tom="info" rotulo={`Funções (${existentes} já existem)`} valor={funcoes.length} />
      <KpiCard tom={resultado.divergencias.length > 0 ? 'atencao' : 'ok'} rotulo="Divergências" valor={resultado.divergencias.length} />
    </div>
  );
}

function Diferencas({ resultado }: { resultado: ResultadoLeituraIa }) {
  const colunas: Coluna<ResultadoLeituraIa['diferencas'][number]>[] = [
    { chave: 'tipo', rotulo: '', render: (d) => <StatusChip tom={tomDiferenca[d.tipo]}>{rotuloDiferenca[d.tipo]}</StatusChip> },
    { chave: 'oque', rotulo: 'O quê', render: (d) => d.oque },
    { chave: 'antes', rotulo: 'Antes', render: (d) => d.antes ?? '—' },
    { chave: 'depois', rotulo: 'Depois', render: (d) => d.depois ?? '—' },
  ];
  return (
    <Card titulo="O que muda em relação à estrutura atual" subtitulo="A estrutura atual sai de uso (fica guardada) ao cadastrar">
      <DataTable
        aria-label="Diferenças em relação à estrutura atual"
        densidade="compacta"
        colunas={colunas}
        linhas={resultado.diferencas}
        chaveLinha={(d) => `${d.tipo}-${d.oque}`}
        vazio={{ titulo: 'Nenhuma diferença em relação à estrutura atual.' }}
      />
    </Card>
  );
}

function TabelaFuncoes({
  resultado,
  funcoesSistema,
  escolhas,
  aoEscolher,
  editavel,
}: {
  resultado: ResultadoLeituraIa;
  funcoesSistema: Funcao[];
  escolhas: Record<string, string>;
  aoEscolher: (nomeDocumento: string, valor: string) => void;
  editavel: boolean;
}) {
  const colunas: Coluna<FuncaoLidaIa>[] = [
    { chave: 'nome', rotulo: 'No documento', render: (f) => <Text weight="semibold">{f.nomeDocumento}</Text> },
    {
      chave: 'ghe',
      rotulo: resultado.documento === 'PGR' ? 'GHE' : 'Exames',
      render: (f) => (resultado.documento === 'PGR' ? f.ghes.map((n) => String(n).padStart(2, '0')).join(', ') : f.exames),
    },
    {
      chave: 'sistema',
      rotulo: 'No sistema',
      render: (f) => (
        <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
          <StatusChip tom={tomSituacao[f.situacao]}>{rotuloSituacao[f.situacao]}</StatusChip>
          <Select
            aria-label={`Função no sistema para ${f.nomeDocumento}`}
            value={escolhas[f.nomeDocumento] ?? CRIAR}
            onChange={(_, d) => aoEscolher(f.nomeDocumento, d.value)}
            disabled={!editavel}
          >
            <option value={CRIAR}>Criar “{f.nomeDocumento}”</option>
            {funcoesSistema.map((s) => (
              <option key={s.id} value={s.id}>
                {s.nome}
              </option>
            ))}
          </Select>
        </div>
      ),
    },
  ];
  return (
    <Card
      titulo="Funções"
      subtitulo={"Cada função do documento é ligada a uma função do sistema (por exemplo, a criada pelo G-RH). \"Confirme\" = nome parecido."}
    >
      <DataTable aria-label="Funções lidas" colunas={colunas} linhas={resultado.funcoes} chaveLinha={(f) => f.nomeDocumento} />
    </Card>
  );
}

function Divergencias({ resultado }: { resultado: ResultadoLeituraIa }) {
  if (resultado.divergencias.length === 0) return null;
  return (
    <Card titulo="Divergências encontradas" subtitulo="Viram pendências de validação técnica ao cadastrar">
      <div style={{ display: 'grid', gap: 6 }}>
        {resultado.divergencias.map((d, i) => (
          <div
            key={i}
            style={{
              borderLeft: `3px solid ${d.gravidade === 'alta' ? tokensUi.status.alerta.tinta : tokensUi.status.atencao.tinta}`,
              background: d.gravidade === 'alta' ? tokensUi.status.alerta.fundo : tokensUi.status.atencao.fundo,
              padding: '6px 10px',
              borderRadius: '0 6px 6px 0',
            }}
          >
            <Text size={300}>{d.texto}</Text>
          </div>
        ))}
      </div>
    </Card>
  );
}

function RiscosPorGhe({ ghes, aberto, aoAbrir }: { ghes: GheLidoIa[]; aberto: number | null; aoAbrir: (n: number | null) => void }) {
  const colunasGhe: Coluna<GheLidoIa>[] = [
    { chave: 'numero', rotulo: 'GHE', render: (g) => <Text weight="semibold">GHE {String(g.numero).padStart(2, '0')}</Text> },
    { chave: 'funcoes', rotulo: 'Funções', render: (g) => g.funcoes.map((f) => f.nome).join(', ') },
    { chave: 'setor', rotulo: 'Setor', render: (g) => g.setor ?? '—' },
    { chave: 'riscos', rotulo: 'Riscos', render: (g) => g.riscos.filter((r) => r.probabilidade > 0 && r.severidade > 0).length },
  ];
  type Risco = GheLidoIa['riscos'][number];
  const colunasRisco: Coluna<Risco>[] = [
    { chave: 'agente', rotulo: 'Perigo', render: (r) => <Text weight="semibold">{r.agente}</Text> },
    { chave: 'tipo', rotulo: 'Tipo', render: (r) => r.tipo },
    { chave: 'ps', rotulo: 'P × S', render: (r) => (r.probabilidade > 0 ? `${r.probabilidade} × ${r.severidade}` : 'sem exposição') },
    { chave: 'classificacao', rotulo: 'No documento', render: (r) => r.classificacaoDocumento ?? '—' },
    { chave: 'epi', rotulo: 'EPI', render: (r) => r.epi ?? '—' },
  ];
  return (
    <Card titulo="Riscos por GHE" subtitulo="Clique no GHE para ver os riscos lidos">
      <DataTable
        aria-label="GHE lidos"
        colunas={colunasGhe}
        linhas={ghes}
        chaveLinha={(g) => String(g.numero)}
        aoClicarLinha={(g) => aoAbrir(aberto === g.numero ? null : g.numero)}
        expansivel={{
          aberta: (g) => aberto === g.numero,
          render: (g) => (
            <DataTable
              aria-label={`Riscos do GHE ${g.numero}`}
              densidade="compacta"
              colunas={colunasRisco}
              linhas={g.riscos}
              chaveLinha={(r) => `${r.agente}-${r.probabilidade}-${r.severidade}`}
            />
          ),
        }}
      />
    </Card>
  );
}

function ExamesLidos({ resultado }: { resultado: ResultadoLeituraIa }) {
  const linhas = useMemo(
    () => resultado.examesPorFuncao.flatMap((f) => f.exames.map((e) => ({ funcao: f.funcao, ...e }))),
    [resultado],
  );
  type Linha = (typeof linhas)[number];
  const colunas: Coluna<Linha>[] = [
    { chave: 'funcao', rotulo: 'Função', render: (l) => <Text weight="semibold">{l.funcao}</Text> },
    { chave: 'exame', rotulo: 'Exame', render: (l) => `${nomeExame(l.exame)}${l.codigo ? ` · ${l.codigo}` : ''}` },
    { chave: 'periodico', rotulo: 'Periódico', render: (l) => (l.periodico ? rotuloPeriodicidade(l.periodicidadeMeses) : '—') },
    {
      chave: 'asos',
      rotulo: 'Entra no ASO',
      render: (l) =>
        [
          l.admissional && 'Admissional',
          l.periodico && 'Periódico',
          l.retornoTrabalho && 'Retorno',
          l.mudancaRisco && 'Mudança de risco',
          l.demissional && 'Demissional',
        ]
          .filter(Boolean)
          .join(', '),
    },
  ];
  return (
    <Card titulo="Exames por função lidos">
      <DataTable
        aria-label="Exames lidos"
        densidade="compacta"
        colunas={colunas}
        linhas={linhas}
        chaveLinha={(l) => `${l.funcao}-${l.exame}`}
      />
    </Card>
  );
}
