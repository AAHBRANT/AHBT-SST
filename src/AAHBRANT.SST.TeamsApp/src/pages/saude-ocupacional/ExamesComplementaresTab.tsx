import { useEffect, useState } from 'react';
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
  StatusChip,
  Text,
  designTokens,
  nivelVencimento,
  rotuloDeVencimento,
  tomDeVencimento,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular, Save24Regular } from '@fluentui/react-icons';
import {
  api,
  tipoExameComplementarLabel,
  type Aso,
  type ExameComplementar,
  type NovoExameComplementar,
  type Trabalhador,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

function exameVazio(): NovoExameComplementar {
  return {
    trabalhadorId: '',
    asoId: '',
    tipo: 1,
    dataRealizacao: '',
    dataValidade: '',
    resultado: '',
    observacoes: '',
    responsavelTecnico: '',
  };
}

function chipVencimento(data?: string | null) {
  const nivel = nivelVencimento(data);
  return nivel ? <StatusChip tom={tomDeVencimento(nivel)}>{rotuloDeVencimento(nivel)}</StatusChip> : null;
}

// Onda 2 Task 12 (camada ui/): exames complementares do PCMSO (audiometria, acuidade visual etc.),
// vinculados opcionalmente a um ASO. Sem chip de resultado — `resultado` é texto livre, não enum, ao
// contrário de AsosTab.tsx/AptidoesTab.tsx. Edição inline por linha, mesmo padrão das outras abas.
export function ExamesComplementaresTab() {
  const [exames, setExames] = useState<ExameComplementar[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [asos, setAsos] = useState<Aso[]>([]);
  const [novoExame, setNovoExame] = useState<NovoExameComplementar>(exameVazio());
  const [edicaoId, setEdicaoId] = useState<string | null>(null);
  const [edicao, setEdicao] = useState<ExameComplementar | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [lista, listaTrabalhadores, listaAsos] = await Promise.all([
        api.examesComplementares.listar(),
        api.trabalhadores.listar(),
        api.asos.listar(),
      ]);
      setExames(lista);
      setTrabalhadores(listaTrabalhadores);
      setAsos(listaAsos);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar exames complementares.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function nomeTrabalhador(id: string) {
    return trabalhadores.find((t) => t.id === id)?.nome ?? id;
  }

  const asosDoTrabalhadorSelecionado = asos.filter((a) => a.trabalhadorId === novoExame.trabalhadorId);

  async function criar() {
    if (!novoExame.trabalhadorId || !novoExame.dataRealizacao || !novoExame.dataValidade || !novoExame.resultado.trim()) {
      setErro('Preencha funcionário, datas e resultado.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.examesComplementares.criar({ ...novoExame, asoId: novoExame.asoId || null });
      setNovoExame(exameVazio());
      await carregar();
      sucessoToast('Exame complementar registrado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar exame complementar.');
    } finally {
      setCarregando(false);
    }
  }

  function iniciarEdicao(exame: ExameComplementar) {
    setEdicaoId(exame.id);
    setEdicao({ ...exame });
  }

  async function salvarEdicao() {
    if (!edicao) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.examesComplementares.atualizar(edicao);
      setEdicaoId(null);
      setEdicao(null);
      await carregar();
      sucessoToast('Exame complementar atualizado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao atualizar exame complementar.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este exame complementar? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.examesComplementares.excluir(id);
      await carregar();
      sucessoToast('Exame complementar excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir exame complementar.');
    }
  }

  const colunas: Coluna<ExameComplementar>[] = [
    { chave: 'trabalhador', rotulo: 'Funcionário', render: (ex) => nomeTrabalhador(ex.trabalhadorId) },
    {
      chave: 'tipo',
      rotulo: 'Tipo',
      render: (ex) =>
        edicaoId === ex.id && edicao ? (
          <Select value={edicao.tipo} onChange={(_, d) => setEdicao({ ...edicao, tipo: Number(d.value) })}>
            {Object.entries(tipoExameComplementarLabel).map(([valor, rotulo]) => (
              <option key={valor} value={valor}>
                {rotulo}
              </option>
            ))}
          </Select>
        ) : (
          tipoExameComplementarLabel[ex.tipo]
        ),
    },
    {
      chave: 'realizacao',
      rotulo: 'Realização',
      render: (ex) =>
        edicaoId === ex.id && edicao ? (
          <CampoData
            value={edicao.dataRealizacao?.slice(0, 10)}
            onChange={(_, d) => setEdicao({ ...edicao, dataRealizacao: d.value })}
          />
        ) : (
          ex.dataRealizacao?.slice(0, 10)
        ),
    },
    {
      chave: 'validade',
      rotulo: 'Validade',
      render: (ex) =>
        edicaoId === ex.id && edicao ? (
          <CampoData
            value={edicao.dataValidade?.slice(0, 10)}
            onChange={(_, d) => setEdicao({ ...edicao, dataValidade: d.value })}
          />
        ) : (
          <>
            {ex.dataValidade?.slice(0, 10)} {chipVencimento(ex.dataValidade)}
          </>
        ),
    },
    {
      chave: 'resultado',
      rotulo: 'Resultado',
      render: (ex) =>
        edicaoId === ex.id && edicao ? (
          <Input value={edicao.resultado} onChange={(_, d) => setEdicao({ ...edicao, resultado: d.value })} />
        ) : (
          ex.resultado
        ),
    },
  ];

  return (
    <>
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <Card titulo="Exames Complementares">
        <FormSection titulo="Novo exame complementar" numero={1} primeira>
          <FormGrid>
            <Campo span={3}>
              <Field label="Funcionário">
                <Select
                  value={novoExame.trabalhadorId}
                  onChange={(_, d) => setNovoExame({ ...novoExame, trabalhadorId: d.value, asoId: '' })}
                >
                  <option value="">Selecione</option>
                  {trabalhadores.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.nome} ({t.matricula})
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="ASO vinculado (opcional)">
                <Select
                  value={novoExame.asoId ?? ''}
                  onChange={(_, d) => setNovoExame({ ...novoExame, asoId: d.value })}
                  disabled={!novoExame.trabalhadorId}
                >
                  <option value="">Nenhum</option>
                  {asosDoTrabalhadorSelecionado.map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.dataExame?.slice(0, 10)}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Tipo de exame">
                <Select value={novoExame.tipo} onChange={(_, d) => setNovoExame({ ...novoExame, tipo: Number(d.value) })}>
                  {Object.entries(tipoExameComplementarLabel).map(([valor, rotulo]) => (
                    <option key={valor} value={valor}>
                      {rotulo}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Data de realização">
                <CampoData
                  value={novoExame.dataRealizacao}
                  onChange={(_, d) => setNovoExame({ ...novoExame, dataRealizacao: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Validade">
                <CampoData
                  value={novoExame.dataValidade}
                  onChange={(_, d) => setNovoExame({ ...novoExame, dataValidade: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Resultado">
                <Input
                  value={novoExame.resultado}
                  onChange={(_, d) => setNovoExame({ ...novoExame, resultado: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Responsável técnico">
                <Input
                  value={novoExame.responsavelTecnico ?? ''}
                  onChange={(_, d) => setNovoExame({ ...novoExame, responsavelTecnico: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Observações">
                <Input
                  value={novoExame.observacoes ?? ''}
                  onChange={(_, d) => setNovoExame({ ...novoExame, observacoes: d.value })}
                />
              </Field>
            </Campo>
          </FormGrid>
          <FormRodape>
            <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
              Registrar exame
            </Button>
          </FormRodape>
        </FormSection>

        <DataTable
          aria-label="Exames complementares registrados"
          colunas={colunas}
          linhas={exames}
          chaveLinha={(ex) => ex.id}
          carregando={carregandoLista}
          vazio={{ titulo: 'Nenhum exame complementar cadastrado ainda.' }}
          aoClicarLinha={(ex) => {
            if (edicaoId !== ex.id) iniciarEdicao(ex);
          }}
          acoesLinha={(ex) =>
            edicaoId === ex.id ? (
              <Button
                appearance="subtle"
                icon={<Save24Regular />}
                onClick={salvarEdicao}
                disabled={carregando}
                aria-label="Salvar"
              />
            ) : (
              <Button
                appearance="subtle"
                icon={<Delete24Regular />}
                onClick={() => excluir(ex.id)}
                aria-label="Excluir"
              />
            )
          }
        />
        <Text size={200} style={{ color: designTokens.colorNeutralMedium, display: 'block', marginTop: 8 }}>
          Clique em uma linha para editar o exame complementar.
        </Text>
      </Card>
      {dialogElement}
    </>
  );
}
