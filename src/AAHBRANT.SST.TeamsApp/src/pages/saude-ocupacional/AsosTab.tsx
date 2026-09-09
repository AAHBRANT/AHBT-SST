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
  tipoExameAsoLabel,
  type Aso,
  type NovoAso,
  type Trabalhador,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

function asoVazio(): NovoAso {
  return {
    trabalhadorId: '',
    tipo: 1,
    dataExame: '',
    dataValidade: '',
    resultadoStatus: ResultadoAso.Pendente,
    medicoNome: '',
    medicoCrm: '',
    observacoesClinicas: '',
  };
}

// ResultadoAso: Apto=1, AptoComRestricao=2, Inapto=3, Pendente=4 — mesmo enum reaproveitado por
// AptidoesTab.tsx, mesma tabela de tons.
const tomPorResultadoAso: Record<number, Tom> = { 1: 'ok', 2: 'atencao', 3: 'alerta', 4: 'info' };

function chipVencimento(data?: string | null) {
  const nivel = nivelVencimento(data);
  return nivel ? <StatusChip tom={tomDeVencimento(nivel)}>{rotuloDeVencimento(nivel)}</StatusChip> : null;
}

// Onda 2 Task 12 (camada ui/): cross-worker (todos os trabalhadores) — a versão somente-leitura
// escopada a UM trabalhador já existe em PerfilGeralTab.tsx (aba "Geral & ASO" do perfil), que
// continua inalterada. Edição inline por linha: clicar na linha abre os campos editáveis nela mesma;
// clicar de novo enquanto já está em edição não reinicia a edição (guarda em aoClicarLinha).
export function AsosTab() {
  const [asos, setAsos] = useState<Aso[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [novoAso, setNovoAso] = useState<NovoAso>(asoVazio());
  const [edicaoId, setEdicaoId] = useState<string | null>(null);
  const [edicao, setEdicao] = useState<Aso | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [lista, listaTrabalhadores] = await Promise.all([api.asos.listar(), api.trabalhadores.listar()]);
      setAsos(lista);
      setTrabalhadores(listaTrabalhadores);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar ASOs.');
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
    if (!novoAso.trabalhadorId || !novoAso.dataExame || !novoAso.dataValidade) {
      setErro('Preencha funcionário, data do exame e validade.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.asos.criar(novoAso);
      setNovoAso(asoVazio());
      await carregar();
      sucessoToast('ASO registrado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar ASO.');
    } finally {
      setCarregando(false);
    }
  }

  function iniciarEdicao(aso: Aso) {
    setEdicaoId(aso.id);
    setEdicao({ ...aso });
  }

  async function salvarEdicao() {
    if (!edicao) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.asos.atualizar(edicao);
      setEdicaoId(null);
      setEdicao(null);
      await carregar();
      sucessoToast('ASO atualizado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao atualizar ASO.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este ASO? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.asos.excluir(id);
      await carregar();
      sucessoToast('ASO excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir ASO.');
    }
  }

  const colunas: Coluna<Aso>[] = [
    { chave: 'trabalhador', rotulo: 'Funcionário', render: (a) => nomeTrabalhador(a.trabalhadorId) },
    {
      chave: 'tipo',
      rotulo: 'Tipo',
      render: (a) =>
        edicaoId === a.id && edicao ? (
          <Select value={edicao.tipo} onChange={(_, d) => setEdicao({ ...edicao, tipo: Number(d.value) })}>
            {Object.entries(tipoExameAsoLabel).map(([valor, rotulo]) => (
              <option key={valor} value={valor}>
                {rotulo}
              </option>
            ))}
          </Select>
        ) : (
          tipoExameAsoLabel[a.tipo]
        ),
    },
    {
      chave: 'exame',
      rotulo: 'Exame',
      render: (a) =>
        edicaoId === a.id && edicao ? (
          <CampoData
            value={edicao.dataExame?.slice(0, 10)}
            onChange={(_, d) => setEdicao({ ...edicao, dataExame: d.value })}
          />
        ) : (
          a.dataExame?.slice(0, 10)
        ),
    },
    {
      chave: 'validade',
      rotulo: 'Validade',
      render: (a) =>
        edicaoId === a.id && edicao ? (
          <CampoData
            value={edicao.dataValidade?.slice(0, 10)}
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
          <Select
            value={edicao.resultadoStatus}
            onChange={(_, d) => setEdicao({ ...edicao, resultadoStatus: Number(d.value) })}
          >
            {Object.entries(resultadoAsoLabel).map(([valor, rotulo]) => (
              <option key={valor} value={valor}>
                {rotulo}
              </option>
            ))}
          </Select>
        ) : (
          <StatusChip tom={tomPorResultadoAso[a.resultadoStatus] ?? 'neutro'}>
            {resultadoAsoLabel[a.resultadoStatus]}
          </StatusChip>
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

      <Card titulo="ASOs">
        <FormSection titulo="Novo ASO" numero={1} primeira>
          <FormGrid>
            <Campo span={4}>
              <Field label="Funcionário">
                <Select
                  value={novoAso.trabalhadorId}
                  onChange={(_, d) => setNovoAso({ ...novoAso, trabalhadorId: d.value })}
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
              <Field label="Tipo de exame">
                <Select value={novoAso.tipo} onChange={(_, d) => setNovoAso({ ...novoAso, tipo: Number(d.value) })}>
                  {Object.entries(tipoExameAsoLabel).map(([valor, rotulo]) => (
                    <option key={valor} value={valor}>
                      {rotulo}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Data do exame">
                <CampoData
                  value={novoAso.dataExame}
                  onChange={(_, d) => setNovoAso({ ...novoAso, dataExame: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Validade">
                <CampoData
                  value={novoAso.dataValidade}
                  onChange={(_, d) => setNovoAso({ ...novoAso, dataValidade: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Resultado">
                <Select
                  value={novoAso.resultadoStatus}
                  onChange={(_, d) => setNovoAso({ ...novoAso, resultadoStatus: Number(d.value) })}
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
              <Field label="Médico">
                <Input
                  value={novoAso.medicoNome ?? ''}
                  onChange={(_, d) => setNovoAso({ ...novoAso, medicoNome: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="CRM">
                <Input
                  value={novoAso.medicoCrm ?? ''}
                  onChange={(_, d) => setNovoAso({ ...novoAso, medicoCrm: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Observações clínicas">
                <Textarea
                  value={novoAso.observacoesClinicas ?? ''}
                  onChange={(_, d) => setNovoAso({ ...novoAso, observacoesClinicas: d.value })}
                />
              </Field>
            </Campo>
          </FormGrid>
          <FormRodape>
            <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
              Registrar ASO
            </Button>
          </FormRodape>
        </FormSection>

        <DataTable
          aria-label="ASOs registrados"
          colunas={colunas}
          linhas={asos}
          chaveLinha={(a) => a.id}
          carregando={carregandoLista}
          vazio={{ titulo: 'Nenhum ASO cadastrado ainda.' }}
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
          Clique em uma linha para editar o ASO.
        </Text>
      </Card>
      {dialogElement}
    </>
  );
}
