import { useEffect, useMemo, useState } from 'react';
import {
  Button,
  Campo,
  Card,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormSection,
  Input,
  PageHeader,
  Select,
  StatusChip,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
  Textarea,
  tokensUi,
  useConfirmar,
  type Coluna,
  type Tom,
} from '@ui';
import { Add24Regular } from '@fluentui/react-icons';
import { api, nivelRiscoLabel, NivelRisco, type MatrizRiscoConfig } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

// Guia de conversão §5 (StatusChip), mesma união de 5 tons do Fluent → 4 tons de @ui, colapsando
// Alto/Crítico no mesmo "alerta" (o rótulo textual continua distinguindo os dois níveis).
const tomPorNivel: Record<number, Tom> = { 1: 'ok', 2: 'info', 3: 'atencao', 4: 'alerta', 5: 'alerta' };

function nivelSugerido(probabilidade: number, severidade: number): number {
  const score = probabilidade + severidade;
  if (score <= 3) return NivelRisco.Trivial;
  if (score <= 5) return NivelRisco.Baixo;
  if (score <= 7) return NivelRisco.Moderado;
  if (score <= 9) return NivelRisco.Alto;
  return NivelRisco.Critico;
}

const data = (iso?: string | null) => (iso ? new Date(iso).toLocaleDateString('pt-BR') : '—');

const chave = (p: number, s: number) => `${p}-${s}`;

// Matriz de risco versionada (25/10/2026). A vigente é a única usada para classificar riscos e aparece só para
// consulta (grade colorida, sem campo editável). Mudar a matriz é criar uma NOVA VERSÃO (revisão): a nova substitui
// a vigente, a anterior vai para o histórico e os riscos já avaliados mantêm o nível que receberam na época.
// Só o Engenheiro de Segurança publica versão; o servidor recusa os demais.

// Grade Probabilidade × Severidade. Em consulta mostra o nível escrito dentro de cada célula; em edição, um Select.
function GradeMatriz({ numP, numS, nivelDe, editavel, aoMudar }: {
  numP: number;
  numS: number;
  nivelDe: (p: number, s: number) => number;
  editavel: boolean;
  aoMudar?: (p: number, s: number, nivel: number) => void;
}) {
  const linhas = useMemo(() => Array.from({ length: numP }, (_, i) => i + 1), [numP]);
  const colunas = useMemo(() => Array.from({ length: numS }, (_, i) => i + 1), [numS]);
  return (
    <div style={{ overflowX: 'auto' }}>
      <Table noNativeElements>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Probabilidade \ Severidade</TableHeaderCell>
            {colunas.map((s) => (
              <TableHeaderCell key={s}>{s}</TableHeaderCell>
            ))}
          </TableRow>
        </TableHeader>
        <TableBody>
          {linhas.map((p) => (
            <TableRow key={p}>
              <TableCell>
                <Text weight="semibold">{p}</Text>
              </TableCell>
              {colunas.map((s) => {
                const nivel = nivelDe(p, s);
                return (
                  <TableCell key={s} style={{ backgroundColor: tokensUi.status[tomPorNivel[nivel]].fundo }}>
                    {editavel ? (
                      <Select
                        aria-label={`Nível de risco para probabilidade ${p} e severidade ${s}`}
                        value={String(nivel)}
                        onChange={(_, d) => aoMudar?.(p, s, Number(d.value))}
                      >
                        {Object.entries(nivelRiscoLabel).map(([valor, rotulo]) => (
                          <option key={valor} value={valor}>
                            {rotulo}
                          </option>
                        ))}
                      </Select>
                    ) : (
                      <Text
                        weight={nivel === NivelRisco.Critico ? 'bold' : 'semibold'}
                        aria-label={`Probabilidade ${p}, severidade ${s}: ${nivelRiscoLabel[nivel]}`}
                      >
                        {nivelRiscoLabel[nivel]}
                      </Text>
                    )}
                  </TableCell>
                );
              })}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

function Legenda() {
  return (
    <div style={{ display: 'flex', gap: 14, flexWrap: 'wrap', marginTop: 10 }}>
      {Object.entries(nivelRiscoLabel).map(([valor, rotulo]) => (
        <span key={valor} style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
          <span style={{ width: 14, height: 14, borderRadius: 3, backgroundColor: tokensUi.status[tomPorNivel[Number(valor)]].fundo, border: '1px solid var(--colorNeutralStroke2)' }} />
          <Text size={200}>{rotulo}</Text>
        </span>
      ))}
    </div>
  );
}

const colunasHistorico: Coluna<MatrizRiscoConfig>[] = [
  { chave: 'versao', rotulo: 'Versão', render: (v) => `v${v.versao}` },
  {
    chave: 'situacao',
    rotulo: 'Situação',
    render: (v) => (v.vigente ? <StatusChip tom="ok">Vigente</StatusChip> : <StatusChip tom="neutro">Substituída</StatusChip>),
  },
  {
    chave: 'vigencia',
    rotulo: 'Vigência',
    render: (v) => (v.substituidaEmUtc ? `${data(v.vigenteDesdeUtc)} a ${data(v.substituidaEmUtc)}` : `desde ${data(v.vigenteDesdeUtc)}`),
  },
  { chave: 'dimensoes', rotulo: 'Dimensões', render: (v) => `${v.numNiveisProbabilidade} × ${v.numNiveisSeveridade}` },
  { chave: 'motivo', rotulo: 'Motivo da revisão', render: (v) => v.motivoRevisao ?? (v.versao === 1 ? 'Versão inicial' : '—') },
  { chave: 'responsavel', rotulo: 'Responsável', render: (v) => v.revisadaPorNome ?? '—' },
];

export function MatrizRiscoTab() {
  const [versoes, setVersoes] = useState<MatrizRiscoConfig[]>([]);
  const [selecionadaId, setSelecionadaId] = useState<string | null>(null);
  const [criando, setCriando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  // Rascunho da nova versão.
  const [nome, setNome] = useState('');
  const [numP, setNumP] = useState(5);
  const [numS, setNumS] = useState(5);
  const [celulas, setCelulas] = useState<Record<string, number>>({});
  const [motivo, setMotivo] = useState('');

  const vigente = versoes.find((v) => v.vigente) ?? null;
  const emConsulta = versoes.find((v) => v.id === selecionadaId) ?? vigente;
  const proximaVersao = (versoes[0]?.versao ?? 0) + 1;

  async function carregar() {
    try {
      setErro(null);
      setVersoes(await api.matrizRisco.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar a matriz de risco.');
    } finally {
      setCarregandoLista(false);
    }
  }

  // Carga inicial: só atualiza estado nos callbacks da requisição (carregar() mexe no estado de forma síncrona).
  useEffect(() => {
    api.matrizRisco
      .listar()
      .then(setVersoes)
      .catch((e) => setErro(e instanceof Error ? e.message : 'Falha ao carregar a matriz de risco.'))
      .finally(() => setCarregandoLista(false));
  }, []);

  // A nova versão parte de uma cópia da vigente: o engenheiro só muda o que precisa mudar.
  function iniciarNovaVersao() {
    const base = vigente;
    setNome(base?.nome ?? 'Matriz de risco');
    setNumP(base?.numNiveisProbabilidade ?? 5);
    setNumS(base?.numNiveisSeveridade ?? 5);
    setCelulas(Object.fromEntries((base?.celulas ?? []).map((c) => [chave(c.probabilidade, c.severidade), c.nivelRisco])));
    setMotivo('');
    setErro(null);
    setCriando(true);
  }

  const nivelDoRascunho = (p: number, s: number) => celulas[chave(p, s)] ?? nivelSugerido(p, s);

  async function publicar() {
    if (vigente && !motivo.trim()) {
      setErro('Informe o motivo da revisão da matriz.');
      return;
    }
    const aviso = vigente
      ? `Publicar a versão ${proximaVersao}? A versão ${vigente.versao} deixa de valer agora e vai para o histórico. Os riscos já avaliados mantêm o nível que receberam.`
      : 'Criar a primeira versão da matriz de risco?';
    // Tom neutro: é uma publicação, não uma exclusão (o padrão do diálogo é "Confirmar exclusão" em vermelho).
    const confirmado = await confirmar({
      titulo: vigente ? `Publicar versão ${proximaVersao}` : 'Criar a primeira matriz',
      mensagem: aviso,
      rotuloConfirmar: 'Publicar',
      tom: 'neutro',
    });
    if (!confirmado) return;
    try {
      setSalvando(true);
      setErro(null);
      const celulasPayload = Array.from({ length: numP }, (_, i) => i + 1).flatMap((p) =>
        Array.from({ length: numS }, (_, i) => i + 1).map((s) => ({ probabilidade: p, severidade: s, nivelRisco: nivelDoRascunho(p, s) })),
      );
      await api.matrizRisco.novaVersao({
        nome: nome.trim(),
        numNiveisProbabilidade: numP,
        numNiveisSeveridade: numS,
        celulas: celulasPayload,
        motivo: motivo.trim() || null,
      });
      setCriando(false);
      setSelecionadaId(null);
      await carregar();
      sucessoToast(vigente ? `Versão ${proximaVersao} da matriz publicada.` : 'Matriz de risco criada.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao publicar a nova versão da matriz.');
    } finally {
      setSalvando(false);
    }
  }

  const consultandoAntiga = emConsulta !== null && !emConsulta.vigente;

  return (
    <div>
      {dialogElement}
      <PageHeader
        titulo="Matriz de Risco (Probabilidade × Severidade)"
        subtitulo="A matriz vigente é a única usada para classificar os riscos e fica somente para consulta. Para mudá-la, crie uma nova versão (revisão): ela substitui a atual, que passa para o histórico. Os riscos já avaliados mantêm o nível que receberam."
        acoes={
          !criando && (
            <Button appearance="primary" icon={<Add24Regular />} onClick={iniciarNovaVersao} disabled={carregandoLista}>
              {vigente ? 'Nova versão (revisão)' : 'Criar a primeira matriz'}
            </Button>
          )
        }
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      {criando ? (
        <Card titulo={vigente ? `Nova versão ${proximaVersao} (substitui a versão ${vigente.versao})` : 'Primeira versão da matriz de risco'}>
          <FormSection titulo="Dados da versão" primeira>
            <FormGrid>
              <Campo span={6}>
                <Field label="Nome da matriz">
                  <Input value={nome} onChange={(_, d) => setNome(d.value)} maxLength={150} />
                </Field>
              </Campo>
              <Campo span={3}>
                <Field label="Níveis de probabilidade">
                  <Input type="number" min={1} max={10} value={String(numP)} onChange={(_, d) => setNumP(Math.min(10, Math.max(1, Number(d.value) || 1)))} />
                </Field>
              </Campo>
              <Campo span={3}>
                <Field label="Níveis de severidade">
                  <Input type="number" min={1} max={10} value={String(numS)} onChange={(_, d) => setNumS(Math.min(10, Math.max(1, Number(d.value) || 1)))} />
                </Field>
              </Campo>
              <Campo span={12}>
                <Field label={vigente ? 'Motivo da revisão (obrigatório)' : 'Observação (opcional)'}>
                  <Textarea
                    value={motivo}
                    maxLength={500}
                    placeholder={vigente ? 'Ex.: ajuste da célula P3 × S4 de Moderado para Alto após revisão do PGR' : ''}
                    onChange={(_, d) => setMotivo(d.value)}
                    style={{ width: '100%' }}
                  />
                </Field>
              </Campo>
            </FormGrid>
          </FormSection>

          <Text size={200} style={{ display: 'block', margin: '8px 0' }}>
            {vigente
              ? 'A grade abaixo começa igual à versão vigente. Ajuste só as células que mudam; ao aumentar a escala, as novas células vêm com uma sugestão (probabilidade + severidade).'
              : 'Os níveis vêm preenchidos com uma sugestão (probabilidade + severidade); ajuste célula a célula conforme a política de risco da empresa.'}
          </Text>
          <GradeMatriz
            numP={numP}
            numS={numS}
            nivelDe={nivelDoRascunho}
            editavel
            aoMudar={(p, s, nivel) => setCelulas((atual) => ({ ...atual, [chave(p, s)]: nivel }))}
          />
          <Legenda />
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 16 }}>
            <Button onClick={() => setCriando(false)} disabled={salvando}>
              Cancelar
            </Button>
            <Button appearance="primary" onClick={publicar} disabled={salvando || !nome.trim()}>
              {vigente ? `Publicar versão ${proximaVersao}` : 'Criar matriz'}
            </Button>
          </div>
        </Card>
      ) : emConsulta ? (
        <Card titulo={`${emConsulta.nome} · versão ${emConsulta.versao}`}>
          <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap', marginBottom: 8 }}>
            {emConsulta.vigente ? <StatusChip tom="ok">Vigente</StatusChip> : <StatusChip tom="atencao">Versão antiga (substituída)</StatusChip>}
            <Text size={200}>
              {emConsulta.substituidaEmUtc
                ? `Valeu de ${data(emConsulta.vigenteDesdeUtc)} a ${data(emConsulta.substituidaEmUtc)}`
                : `Vigente desde ${data(emConsulta.vigenteDesdeUtc)}`}
              {emConsulta.revisadaPorNome ? ` · por ${emConsulta.revisadaPorNome}` : ''}
              {emConsulta.motivoRevisao ? ` · ${emConsulta.motivoRevisao}` : ''}
            </Text>
            {consultandoAntiga && (
              <Button size="small" onClick={() => setSelecionadaId(null)}>
                Voltar para a vigente
              </Button>
            )}
          </div>
          {emConsulta.celulas.length === 0 ? (
            // Nunca mostra uma sugestão como se fosse o dado gravado da versão.
            <FeedbackInline tom="aviso">Esta versão não tem as células gravadas, então não há grade para mostrar.</FeedbackInline>
          ) : (
            <>
              <GradeMatriz
                numP={emConsulta.numNiveisProbabilidade}
                numS={emConsulta.numNiveisSeveridade}
                nivelDe={(p, s) => emConsulta.celulas.find((c) => c.probabilidade === p && c.severidade === s)?.nivelRisco ?? NivelRisco.Trivial}
                editavel={false}
              />
              <Legenda />
            </>
          )}
        </Card>
      ) : (
        !carregandoLista && (
          <Card titulo="Nenhuma matriz de risco cadastrada">
            <Text>Cadastre a primeira matriz para que os riscos possam ser classificados.</Text>
          </Card>
        )
      )}

      {!criando && versoes.length > 0 && (
        <div style={{ marginTop: 16 }}>
          <Card titulo="Histórico de versões">
            <DataTable
              aria-label="Versões da matriz de risco"
              colunas={colunasHistorico}
              linhas={versoes}
              chaveLinha={(v) => v.id}
              carregando={carregandoLista}
              acoesLinha={(v) => (
                <Button size="small" appearance="subtle" onClick={() => setSelecionadaId(v.vigente ? null : v.id)} aria-label={`Ver a versão ${v.versao}`}>
                  Ver
                </Button>
              )}
            />
          </Card>
        </div>
      )}
    </div>
  );
}
