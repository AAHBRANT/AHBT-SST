import { useEffect, useState } from 'react';
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
  Text,
  Textarea,
  designTokens,
  LIMIAR_ALERTA_DIAS,
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

const estiloLinhaApoio = { display: 'block', fontSize: 12, marginTop: 2, color: designTokens.colorNeutralMedium };

// Compara só o dia do calendário: a API manda a validade com hora, e somar a hora/fuso fazia a
// contagem errar por 1 dia.
function diasAteValidade(dataValidade: string) {
  const [ano, mes, dia] = dataValidade.slice(0, 10).split('-').map(Number);
  const hoje = new Date(new Date().toDateString());
  return Math.round((new Date(ano, mes - 1, dia).getTime() - hoje.getTime()) / 86_400_000);
}

function formatarData(iso: string) {
  const [ano, mes, dia] = iso.slice(0, 10).split('-');
  return `${dia}/${mes}/${ano}`;
}

// "Situação hoje" cruza validade e parecer (pedido do usuário, 13/10): antes a linha mostrava
// "Vencido" na validade e "Apto" no resultado lado a lado, e quem lia entendia que o trabalhador
// estava liberado. Vencimento vence qualquer parecer; parecer só decide quando o exame ainda vale.
function situacaoHoje(aso: Aso): { tom: Tom; rotulo: string; detalhe?: string } {
  const dias = diasAteValidade(aso.dataValidade);
  if (dias < 0)
    return { tom: 'alerta', rotulo: 'Vencido', detalhe: `Refazer exame · venceu há ${-dias} ${-dias === 1 ? 'dia' : 'dias'}` };
  if (aso.resultadoStatus === ResultadoAso.Inapto)
    return { tom: 'alerta', rotulo: 'Não liberado', detalhe: 'Inapto no exame' };
  if (aso.resultadoStatus === ResultadoAso.Pendente)
    return { tom: 'info', rotulo: 'Aguardando parecer', detalhe: 'Exame válido, resultado não lançado' };
  if (dias <= LIMIAR_ALERTA_DIAS)
    return { tom: 'atencao', rotulo: dias === 0 ? 'Vence hoje' : `Vence em ${dias} ${dias === 1 ? 'dia' : 'dias'}`, detalhe: 'Agendar periódico' };
  if (aso.resultadoStatus === ResultadoAso.AptoComRestricao) return { tom: 'atencao', rotulo: 'Liberado com restrição' };
  return { tom: 'ok', rotulo: 'Liberado' };
}

// O G-RH às vezes tem o mesmo ASO em vários documentos (ex.: 9 linhas iguais de um trabalhador em
// 13/10), e o SST cria um registro por documento. Mostra uma linha por exame — mesmo trabalhador,
// tipo, data e validade — sem apagar nada do banco; prefere a cópia que já tem parecer lançado.
interface AsoAgrupado extends Aso {
  copias: number;
}

function agruparRepetidos(lista: Aso[]): AsoAgrupado[] {
  const grupos = new Map<string, AsoAgrupado>();
  for (const aso of lista) {
    const chave = [aso.trabalhadorId, aso.tipo, aso.dataExame?.slice(0, 10), aso.dataValidade?.slice(0, 10)].join('|');
    const atual = grupos.get(chave);
    if (!atual) {
      grupos.set(chave, { ...aso, copias: 1 });
    } else if (atual.resultadoStatus === ResultadoAso.Pendente && aso.resultadoStatus !== ResultadoAso.Pendente) {
      grupos.set(chave, { ...aso, copias: atual.copias + 1 });
    } else {
      atual.copias += 1;
    }
  }
  return [...grupos.values()];
}

// Onda 2 Task 12 (camada ui/): cross-worker (todos os trabalhadores) — a versão somente-leitura
// escopada a UM trabalhador já existe em PerfilGeralTab.tsx (aba "Geral & ASO" do perfil), que
// continua inalterada. Edição inline por linha: clicar na linha abre os campos editáveis nela mesma;
// clicar de novo enquanto já está em edição não reinicia a edição (guarda em aoClicarLinha).
export function AsosTab() {
  const [asos, setAsos] = useState<AsoAgrupado[]>([]);
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
      setAsos(agruparRepetidos(lista));
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

  const colunas: Coluna<AsoAgrupado>[] = [
    {
      chave: 'trabalhador',
      rotulo: 'Funcionário',
      render: (a) => (
        <>
          {nomeTrabalhador(a.trabalhadorId)}
          {a.copias > 1 && (
            <span
              style={estiloLinhaApoio}
              title={`O mesmo exame foi recebido ${a.copias} vezes do G-RH. Excluir remove uma cópia por vez.`}
            >
              {a.copias} documentos iguais
            </span>
          )}
        </>
      ),
    },
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
          a.dataValidade?.slice(0, 10)
        ),
    },
    {
      chave: 'situacao',
      rotulo: 'Situação hoje',
      render: (a) => {
        const situacao = situacaoHoje(a);
        return (
          <>
            <StatusChip tom={situacao.tom}>{situacao.rotulo}</StatusChip>
            {situacao.detalhe && <span style={estiloLinhaApoio}>{situacao.detalhe}</span>}
          </>
        );
      },
    },
    {
      chave: 'resultado',
      rotulo: 'Parecer do médico',
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
        ) : diasAteValidade(a.dataValidade) < 0 ? (
          // Exame vencido: o parecer é só histórico, então fica apagado e sem pulsar.
          <>
            <StatusChip tom="neutro">
              {a.resultadoStatus === ResultadoAso.Pendente ? 'Sem parecer' : resultadoAsoLabel[a.resultadoStatus]}
            </StatusChip>
            <span style={estiloLinhaApoio}>no exame de {formatarData(a.dataExame)}</span>
          </>
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
                      {t.matricula ? `${t.nome} (${t.matricula})` : t.nome}
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
              <BotaoAcao tom="excluir" icon={<Delete24Regular />} onClick={() => excluir(a.id)} aria-label="Excluir" />
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
