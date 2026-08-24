import { useEffect, useMemo, useState } from 'react';
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
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, nivelRiscoLabel, NivelRisco, type MatrizRiscoConfig } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

const coresNivel: Record<number, string> = {
  1: '#DFF6DD',
  2: '#D6ECFF',
  3: '#FFF4CE',
  4: '#FFE0C7',
  5: '#FDE2E1',
};

function nivelSugerido(probabilidade: number, severidade: number): number {
  const score = probabilidade + severidade;
  if (score <= 3) return NivelRisco.Trivial;
  if (score <= 5) return NivelRisco.Baixo;
  if (score <= 7) return NivelRisco.Moderado;
  if (score <= 9) return NivelRisco.Alto;
  return NivelRisco.Critico;
}

export function MatrizRiscoTab() {
  const estilos = usePageStyles();
  const [configs, setConfigs] = useState<MatrizRiscoConfig[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

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
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar matriz de risco.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    try {
      await api.matrizRisco.excluir(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir matriz de risco.');
    }
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Nova matriz de risco (Probabilidade × Severidade)</Text>
        </div>

        {erro && <Text className={estilos.erro}>{erro}</Text>}

        <Text size={200} style={{ display: 'block', marginBottom: 12 }}>
          A Base de Conhecimento (§36) exige que a matriz seja configurável pela organização — não há fórmula fixa.
          Os níveis abaixo já vêm preenchidos com uma sugestão (Probabilidade + Severidade); ajuste célula a célula
          conforme a política de risco da empresa antes de salvar.
        </Text>

        <div className={estilos.form}>
          <Field label="Nome da matriz">
            <Input value={nome} onChange={(_, d) => setNome(d.value)} />
          </Field>
          <Field label="Níveis de probabilidade">
            <Input
              type="number"
              min={1}
              max={10}
              value={String(numP)}
              onChange={(_, d) => setNumP(Math.max(1, Number(d.value) || 1))}
            />
          </Field>
          <Field label="Níveis de severidade">
            <Input
              type="number"
              min={1}
              max={10}
              value={String(numS)}
              onChange={(_, d) => setNumS(Math.max(1, Number(d.value) || 1))}
            />
          </Field>
        </div>

        <div style={{ overflowX: 'auto' }}>
          <Table>
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
                  {colunasSeveridade.map((s) => (
                    <TableCell key={s} style={{ backgroundColor: coresNivel[nivelDaCelula(p, s)] }}>
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
                  ))}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>

        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<Add24Regular />} onClick={salvar} disabled={carregando}>
            Salvar matriz
          </Button>
        </div>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Matrizes cadastradas</Text>
        </div>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Nome</TableHeaderCell>
              <TableHeaderCell>Dimensões</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {configs.map((config) => (
              <TableRow key={config.id}>
                <TableCell>{config.nome}</TableCell>
                <TableCell>
                  {config.numNiveisProbabilidade} × {config.numNiveisSeveridade}
                </TableCell>
                <TableCell>
                  <Button
                    appearance="subtle"
                    icon={<Delete24Regular />}
                    onClick={() => excluir(config.id)}
                    aria-label="Excluir"
                  />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
