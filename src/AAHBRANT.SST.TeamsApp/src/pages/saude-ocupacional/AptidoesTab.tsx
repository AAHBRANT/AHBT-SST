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
  Textarea,
  designTokens,
  nivelVencimento,
  rotuloDeVencimento,
  tomDeVencimento,
  useConfirmar,
  type Coluna,
  type Tom,
} from '@ui';
import { Add24Regular, Delete24Regular, Save24Regular } from '@fluentui/react-icons';
import {
  api,
  resultadoAsoLabel,
  ResultadoAso,
  type Aptidao,
  type NovaAptidao,
  type Trabalhador,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

function aptidaoVazia(): NovaAptidao {
  return {
    trabalhadorId: '',
    atividadeCritica: '',
    aptidao: ResultadoAso.Pendente,
    dataAvaliacao: '',
    dataValidade: '',
    medicoResponsavel: '',
    observacoes: '',
  };
}

// ResultadoAso: Apto=1, AptoComRestricao=2, Inapto=3, Pendente=4 — mesmo enum reaproveitado por
// AsosTab.tsx, mesma tabela de tons.
const tomPorResultadoAso: Record<number, Tom> = { 1: 'ok', 2: 'atencao', 3: 'alerta', 4: 'info' };

function chipVencimento(data?: string | null) {
  const nivel = nivelVencimento(data);
  return nivel ? <StatusChip tom={tomDeVencimento(nivel)}>{rotuloDeVencimento(nivel)}</StatusChip> : null;
}

// Onda 2 Task 12 (camada ui/): aptidão para atividade crítica (ex.: trabalho em altura, espaço
// confinado) — distinta do ASO geral, embora reaproveite o mesmo enum de resultado (Apto/Apto com
// restrição/Inapto/Pendente). Edição inline por linha, mesmo padrão de AsosTab.tsx.
export function AptidoesTab() {
  const [aptidoes, setAptidoes] = useState<Aptidao[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [novaAptidao, setNovaAptidao] = useState<NovaAptidao>(aptidaoVazia());
  const [edicaoId, setEdicaoId] = useState<string | null>(null);
  const [edicao, setEdicao] = useState<Aptidao | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [lista, listaTrabalhadores] = await Promise.all([api.aptidoes.listar(), api.trabalhadores.listar()]);
      setAptidoes(lista);
      setTrabalhadores(listaTrabalhadores);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar aptidões.');
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

  async function criar() {
    if (!novaAptidao.trabalhadorId || !novaAptidao.atividadeCritica.trim() || !novaAptidao.dataAvaliacao) {
      setErro('Preencha funcionário, atividade crítica e data da avaliação.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.aptidoes.criar({ ...novaAptidao, dataValidade: novaAptidao.dataValidade || null });
      setNovaAptidao(aptidaoVazia());
      await carregar();
      sucessoToast('Aptidão registrada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar aptidão.');
    } finally {
      setCarregando(false);
    }
  }

  function iniciarEdicao(aptidao: Aptidao) {
    setEdicaoId(aptidao.id);
    setEdicao({ ...aptidao });
  }

  async function salvarEdicao() {
    if (!edicao) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.aptidoes.atualizar(edicao);
      setEdicaoId(null);
      setEdicao(null);
      await carregar();
      sucessoToast('Aptidão atualizada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao atualizar aptidão.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir esta aptidão? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.aptidoes.excluir(id);
      await carregar();
      sucessoToast('Aptidão excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir aptidão.');
    }
  }

  const colunas: Coluna<Aptidao>[] = [
    { chave: 'trabalhador', rotulo: 'Funcionário', render: (a) => nomeTrabalhador(a.trabalhadorId) },
    {
      chave: 'atividadeCritica',
      rotulo: 'Atividade crítica',
      render: (a) =>
        edicaoId === a.id && edicao ? (
          <Input
            value={edicao.atividadeCritica}
            onChange={(_, d) => setEdicao({ ...edicao, atividadeCritica: d.value })}
          />
        ) : (
          a.atividadeCritica
        ),
    },
    {
      chave: 'avaliacao',
      rotulo: 'Avaliação',
      render: (a) =>
        edicaoId === a.id && edicao ? (
          <CampoData
            value={edicao.dataAvaliacao?.slice(0, 10)}
            onChange={(_, d) => setEdicao({ ...edicao, dataAvaliacao: d.value })}
          />
        ) : (
          a.dataAvaliacao?.slice(0, 10)
        ),
    },
    {
      chave: 'validade',
      rotulo: 'Validade',
      render: (a) =>
        edicaoId === a.id && edicao ? (
          <CampoData
            value={edicao.dataValidade?.slice(0, 10) ?? ''}
            onChange={(_, d) => setEdicao({ ...edicao, dataValidade: d.value })}
          />
        ) : (
          <>
            {a.dataValidade?.slice(0, 10)} {chipVencimento(a.dataValidade)}
          </>
        ),
    },
    {
      chave: 'resultado',
      rotulo: 'Resultado',
      render: (a) =>
        edicaoId === a.id && edicao ? (
          <Select value={edicao.aptidao} onChange={(_, d) => setEdicao({ ...edicao, aptidao: Number(d.value) })}>
            {Object.entries(resultadoAsoLabel).map(([valor, rotulo]) => (
              <option key={valor} value={valor}>
                {rotulo}
              </option>
            ))}
          </Select>
        ) : (
          <StatusChip tom={tomPorResultadoAso[a.aptidao] ?? 'neutro'}>{resultadoAsoLabel[a.aptidao]}</StatusChip>
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

      <Card titulo="Aptidões">
        <FormSection titulo="Nova aptidão para atividade crítica" numero={1} primeira>
          <FormGrid>
            <Campo span={4}>
              <Field label="Funcionário">
                <Select
                  value={novaAptidao.trabalhadorId}
                  onChange={(_, d) => setNovaAptidao({ ...novaAptidao, trabalhadorId: d.value })}
                >
                  <option value="">Selecione</option>
                  {trabalhadores.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.matricula ? `${t.nome} (${t.matricula})` : t.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Atividade crítica">
                <Input
                  placeholder="Ex.: Trabalho em altura, Espaço confinado"
                  value={novaAptidao.atividadeCritica}
                  onChange={(_, d) => setNovaAptidao({ ...novaAptidao, atividadeCritica: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Data da avaliação">
                <CampoData
                  value={novaAptidao.dataAvaliacao}
                  onChange={(_, d) => setNovaAptidao({ ...novaAptidao, dataAvaliacao: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Validade (opcional)">
                <CampoData
                  value={novaAptidao.dataValidade ?? ''}
                  onChange={(_, d) => setNovaAptidao({ ...novaAptidao, dataValidade: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Resultado">
                <Select
                  value={novaAptidao.aptidao}
                  onChange={(_, d) => setNovaAptidao({ ...novaAptidao, aptidao: Number(d.value) })}
                >
                  {Object.entries(resultadoAsoLabel).map(([valor, rotulo]) => (
                    <option key={valor} value={valor}>
                      {rotulo}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Médico responsável">
                <Input
                  value={novaAptidao.medicoResponsavel ?? ''}
                  onChange={(_, d) => setNovaAptidao({ ...novaAptidao, medicoResponsavel: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Observações">
                <Textarea
                  value={novaAptidao.observacoes ?? ''}
                  onChange={(_, d) => setNovaAptidao({ ...novaAptidao, observacoes: d.value })}
                />
              </Field>
            </Campo>
          </FormGrid>
          <FormRodape>
            <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
              Registrar aptidão
            </Button>
          </FormRodape>
        </FormSection>

        <DataTable
          aria-label="Aptidões registradas"
          colunas={colunas}
          linhas={aptidoes}
          chaveLinha={(a) => a.id}
          carregando={carregandoLista}
          vazio={{ titulo: 'Nenhuma aptidão cadastrada ainda.' }}
          aoClicarLinha={(a) => {
            if (edicaoId !== a.id) iniciarEdicao(a);
          }}
          acoesLinha={(a) =>
            edicaoId === a.id ? (
              <Button
                appearance="subtle"
                icon={<Save24Regular />}
                onClick={salvarEdicao}
                disabled={carregando}
                aria-label="Salvar"
              />
            ) : (
              <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(a.id)} aria-label="Excluir" />
            )
          }
        />
        <Text size={200} style={{ color: designTokens.colorNeutralMedium, display: 'block', marginTop: 8 }}>
          Clique em uma linha para editar a aptidão.
        </Text>
      </Card>
      {dialogElement}
    </>
  );
}
