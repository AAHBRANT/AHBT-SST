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
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
  tokensUi,
  useConfirmar,
  type Coluna,
  type Tom,
} from '@ui';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
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

const colunasConfigs: Coluna<MatrizRiscoConfig>[] = [
  { chave: 'nome', rotulo: 'Nome' },
  {
    chave: 'dimensoes',
    rotulo: 'Dimensões',
    render: (c) => `${c.numNiveisProbabilidade} × ${c.numNiveisSeveridade}`,
  },
];

// Camada ui/ (Onda 2, Task 13). Julgamento registrado (guia §5.1, spec §4.5 "Matriz"): a grade
// Probabilidade × Severidade abaixo é um heatmap genuíno — célula = <Select>, sem noção de "linha =
// item de dado com colunas fixas" — estruturalmente diferente de MatrizEpiTab/MatrizTreinamentoTab
// (linha-por-item com expansão). DataTable não serve aqui; a grade continua Table/TableRow/TableCell,
// só que importados de @ui (exceção pontual adicionada ao barril nesta mesma task) em vez de
// @fluentui/react-components direto. A segunda seção ("Matrizes cadastradas", lista simples de
// nome+dimensões+excluir) essa sim é DataTable de verdade — não tem nenhuma característica de grade.
// Cor de nível: fundo de célula (não StatusChip) — StatusChip é um chip de conteúdo inline, não uma
// tinta de área; aqui a cor É o dado (indicador visual do nível sugerido/escolhido para aquele
// cruzamento P×S), então vira backgroundColor lido de tokensUi.status[tom].fundo (mesma família
// "lavada" que o StatusChip usa internamente) em vez do hex cru que a página tinha antes.
export function MatrizRiscoTab() {
  const [configs, setConfigs] = useState<MatrizRiscoConfig[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  const [nome, setNome] = useState('Matriz de risco padrão');
  const [numP, setNumP] = useState(5);
  const [numS, setNumS] = useState(5);
  const [celulas, setCelulas] = useState<Record<string, number>>({});

  const linhasProbabilidade = useMemo(() => Array.from({ length: numP }, (_, i) => i + 1), [numP]);
  const colunasSeveridade = useMemo(() => Array.from({ length: numS }, (_, i) => i + 1), [numS]);

  function chave(p: number, s: number) {
    return `${p}-${s}`;
  }

  function nivelDaCelula(p: number, s: number): number {
    return celulas[chave(p, s)] ?? nivelSugerido(p, s);
  }

  function definirCelula(p: number, s: number, nivel: number) {
    setCelulas((atual) => ({ ...atual, [chave(p, s)]: nivel }));
  }

  async function carregar() {
    try {
      setErro(null);
      setConfigs(await api.matrizRisco.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar matrizes de risco.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  async function salvar() {
    try {
      setCarregando(true);
      setErro(null);
      const celulasPayload = linhasProbabilidade.flatMap((p) =>
        colunasSeveridade.map((s) => ({ probabilidade: p, severidade: s, nivelRisco: nivelDaCelula(p, s) })),
      );
      await api.matrizRisco.criar({
        nome,
        numNiveisProbabilidade: numP,
        numNiveisSeveridade: numS,
        celulas: celulasPayload,
      });
      setCelulas({});
      await carregar();
      sucessoToast('Matriz de risco criada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar matriz de risco.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir esta matriz de risco? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.matrizRisco.excluir(id);
      await carregar();
      sucessoToast('Matriz de risco excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir matriz de risco.');
    }
  }

  return (
    <div>
      {dialogElement}
      <PageHeader
        titulo="Matriz de Risco (Probabilidade × Severidade)"
        subtitulo="A Base de Conhecimento (§36) exige que a matriz seja configurável pela organização — não há fórmula fixa. Os níveis abaixo já vêm preenchidos com uma sugestão (Probabilidade + Severidade); ajuste célula a célula conforme a política de risco da empresa antes de salvar."
        acoes={
          <Button appearance="primary" icon={<Add24Regular />} onClick={salvar} disabled={carregando}>
            Salvar matriz
          </Button>
        }
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <Card titulo="Nova matriz de risco">
        <FormSection titulo="Dados da Matriz de Risco" primeira>
          <FormGrid>
            <Campo span={6}>
              <Field label="Nome da matriz">
                <Input value={nome} onChange={(_, d) => setNome(d.value)} />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Níveis de probabilidade">
                <Input
                  type="number"
                  min={1}
                  max={10}
                  value={String(numP)}
                  onChange={(_, d) => setNumP(Math.max(1, Number(d.value) || 1))}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Níveis de severidade">
                <Input
                  type="number"
                  min={1}
                  max={10}
                  value={String(numS)}
                  onChange={(_, d) => setNumS(Math.max(1, Number(d.value) || 1))}
                />
              </Field>
            </Campo>
          </FormGrid>
        </FormSection>

        <div style={{ overflowX: 'auto' }}>
          <Table noNativeElements>
            <TableHeader>
              <TableRow>
                <TableHeaderCell>Probabilidade \ Severidade</TableHeaderCell>
                {colunasSeveridade.map((s) => (
                  <TableHeaderCell key={s}>{s}</TableHeaderCell>
                ))}
              </TableRow>
            </TableHeader>
            <TableBody>
              {linhasProbabilidade.map((p) => (
                <TableRow key={p}>
                  <TableCell>
                    <Text weight="semibold">{p}</Text>
                  </TableCell>
                  {colunasSeveridade.map((s) => {
                    const tom = tomPorNivel[nivelDaCelula(p, s)];
                    return (
                      <TableCell key={s} style={{ backgroundColor: tokensUi.status[tom].fundo }}>
                        <Select
                          value={String(nivelDaCelula(p, s))}
                          onChange={(_, d) => definirCelula(p, s, Number(d.value))}
                        >
                          {Object.entries(nivelRiscoLabel).map(([valor, rotulo]) => (
                            <option key={valor} value={valor}>
                              {rotulo}
                            </option>
                          ))}
                        </Select>
                      </TableCell>
                    );
                  })}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      </Card>

      <div style={{ marginTop: 16 }}>
        <Card titulo="Matrizes cadastradas">
          <DataTable
            aria-label="Matrizes de risco cadastradas"
            colunas={colunasConfigs}
            linhas={configs}
            chaveLinha={(c) => c.id}
            carregando={carregandoLista}
            vazio={{ titulo: 'Nenhuma matriz de risco cadastrada ainda.' }}
            acoesLinha={(c) => (
              <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(c.id)} aria-label="Excluir" />
            )}
          />
        </Card>
      </div>
    </div>
  );
}
