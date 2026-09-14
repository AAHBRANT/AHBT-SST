import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Avatar,
  Button,
  Card,
  PageHeader,
  DataTable,
  StatusChip,
  EstadoVazio,
  FeedbackInline,
  PainelCriacaoInline,
  FormGrid,
  FormRodape,
  FormSection,
  Campo,
  Field,
  Input,
  Select,
  CampoData,
  useConfirmar,
  type Coluna,
  type Tom,
} from '@ui';
import { Add24Regular, ArrowLeft24Regular, Fingerprint24Regular, PeopleTeam24Regular, Search24Regular } from '@fluentui/react-icons';
import { SeletorFotoCamera } from '../../components/SeletorFotoCamera';
import {
  api,
  resultadoAsoLabel,
  ResultadoAso,
  tipoVinculoLabel,
  TipoVinculo,
  type Aso,
  type Funcao,
  type NovoTrabalhador,
  type Obra,
  type Trabalhador,
} from '../../lib/api';
import { formatarCpf } from '../../lib/cpf';
import { CadastroDigitalDialog } from '../../components/pessoas/CadastroDigitalDialog';
import { RequisitosFuncaoDialog } from '../../components/pessoas/RequisitosFuncaoDialog';
import { useSucessoToast } from '../../hooks/useSucessoToast';

const tomAso: Record<number, Tom> = {
  [ResultadoAso.Apto]: 'ok',
  [ResultadoAso.AptoComRestricao]: 'atencao',
  [ResultadoAso.Inapto]: 'alerta',
};

function ultimoAso(asos: Aso[], trabalhadorId: string): Aso | undefined {
  return asos
    .filter((a) => a.trabalhadorId === trabalhadorId)
    .sort((a, b) => new Date(b.dataExame).getTime() - new Date(a.dataExame).getTime())[0];
}

const trabalhadorVazio: NovoTrabalhador = {
  obraId: '',
  setorId: null,
  equipeId: null,
  funcaoId: '',
  nome: '',
  matricula: '',
  cpf: '',
  vinculo: TipoVinculo.Clt,
  dataAdmissao: '',
  turno: '',
};

// Lista de funcionários — piloto 1 estabeleceu o padrão (spec §4.2): o formulário de cadastro sai da
// lista (que hoje empurrava a tabela pra baixo) e vira um painel próprio, com erro isolado do erro de
// carga da lista (regra dos 3 pilotos: erro de painel é estado PRÓPRIO). A lista em si já era um
// cartão-por-linha construído à mão — vira DataTable, que formaliza exatamente esse visual.
// Migração do formulário inline (spec 2026-09-11): o PainelLateral (drawer, com largura="lg") saiu —
// mesmo padrão de AtividadesTab.tsx/InspecoesTab.tsx. Agora é um PainelCriacaoInline, que cresce
// acima da lista até a altura do próprio formulário, em vez de cobrir a tela com uma gaveta larga.
export function TrabalhadoresTab() {
  const navigate = useNavigate();
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [asos, setAsos] = useState<Aso[]>([]);
  const [busca, setBusca] = useState('');
  const [novoTrabalhador, setNovoTrabalhador] = useState<NovoTrabalhador>(trabalhadorVazio);
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const [fotoUrls, setFotoUrls] = useState<Record<string, string>>({});
  const [trabalhadorDigitalAlvo, setTrabalhadorDigitalAlvo] = useState<{ id: string; nome: string } | null>(
    null,
  );
  const [trabalhadorRequisitosAlvo, setTrabalhadorRequisitosAlvo] = useState<{
    id: string;
    nome: string;
    funcaoId: string;
  } | null>(null);
  const [obraSelecionadaId, setObraSelecionadaId] = useState<string | null>(null);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [trabs, obs, funcs, asosResp] = await Promise.all([
        api.trabalhadores.listar(),
        api.obras.listar(),
        api.funcoes.listar(),
        api.asos.listar(),
      ]);
      setTrabalhadores(trabs);
      setObras(obs);
      setFuncoes(funcs);
      setAsos(asosResp);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar funcionários.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  // Miniaturas da foto são baixadas sob demanda (só para trabalhadores com temFoto) e mantidas como
  // object URL até a página ser desmontada — mesmo padrão de ObrasPage.baixarLogo.
  useEffect(() => {
    let cancelado = false;
    (async () => {
      for (const trabalhador of trabalhadores) {
        if (!trabalhador.temFoto || fotoUrls[trabalhador.id]) continue;
        try {
          const blob = await api.trabalhadores.baixarFoto(trabalhador.id);
          if (cancelado) return;
          setFotoUrls((atual) => ({ ...atual, [trabalhador.id]: URL.createObjectURL(blob) }));
        } catch {
          // Falha ao carregar miniatura não impede o uso da página; o trabalhador fica sem foto.
        }
      }
    })();
    return () => {
      cancelado = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trabalhadores]);

  useEffect(() => {
    return () => {
      Object.values(fotoUrls).forEach((url) => URL.revokeObjectURL(url));
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function enviarFoto(trabalhadorId: string, arquivo: File) {
    try {
      setErro(null);
      await api.trabalhadores.enviarFoto(trabalhadorId, arquivo);
      setFotoUrls((atual) => {
        const anterior = atual[trabalhadorId];
        if (anterior) URL.revokeObjectURL(anterior);
        const { [trabalhadorId]: _removido, ...resto } = atual;
        return resto;
      });
      await carregar();
      sucessoToast('Foto enviada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao enviar a foto.');
    }
  }

  function nomeFuncao(id: string) {
    return funcoes.find((f) => f.id === id)?.nome ?? id;
  }

  // Resumo por obra (spec 14/09): cada obra vira um card com a contagem dos 4 vínculos, calculada
  // no cliente a partir da lista já carregada — mesmo padrão de agregação client-side do
  // PessoasDashboardTab, sem endpoint próprio.
  const resumoPorObra = useMemo(() => {
    const mapa = new Map<string, Record<number, number>>();
    for (const t of trabalhadores) {
      const contagem = mapa.get(t.obraId) ?? {};
      contagem[t.vinculo] = (contagem[t.vinculo] ?? 0) + 1;
      mapa.set(t.obraId, contagem);
    }
    return mapa;
  }, [trabalhadores]);

  const obraSelecionada = obras.find((o) => o.id === obraSelecionadaId) ?? null;

  const trabalhadoresFiltrados = useMemo(() => {
    const termo = busca.trim().toLowerCase();
    return trabalhadores.filter((t) => {
      if (obraSelecionadaId && t.obraId !== obraSelecionadaId) return false;
      if (!termo) return true;
      return t.nome.toLowerCase().includes(termo) || (t.matricula ?? '').toLowerCase().includes(termo);
    });
  }, [busca, trabalhadores, obraSelecionadaId]);

  function abrirObra(obraId: string) {
    setObraSelecionadaId(obraId);
    setBusca('');
  }

  function voltarParaObras() {
    setObraSelecionadaId(null);
    setBusca('');
    fecharPainel();
  }

  function abrirPainelNovoFuncionario() {
    setNovoTrabalhador((atual) => ({ ...atual, obraId: obraSelecionadaId ?? atual.obraId }));
    setPainelAberto(true);
  }

  // Todo caminho de fechar o painel limpa o formulário e o erro dele — senão reabrir mostra rascunho
  // e mensagem de uma tentativa anterior.
  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
    setNovoTrabalhador(trabalhadorVazio);
  }

  async function criar() {
    try {
      setCarregando(true);
      setErroPainel(null);
      const { id } = await api.trabalhadores.criar(novoTrabalhador);
      setTrabalhadorRequisitosAlvo({ id, nome: novoTrabalhador.nome, funcaoId: novoTrabalhador.funcaoId });
      setNovoTrabalhador(trabalhadorVazio);
      setPainelAberto(false);
      await carregar();
      sucessoToast('Funcionário criado com sucesso.');
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar funcionário.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (
      !(await confirmar(
        'Excluir este funcionário? Todo o histórico associado (ASO, treinamentos, EPI) fica desvinculado. Essa ação não pode ser desfeita.',
      ))
    )
      return;
    try {
      await api.trabalhadores.excluir(id);
      await carregar();
      sucessoToast('Funcionário excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir funcionário.');
    }
  }

  const colunas: Coluna<Trabalhador>[] = [
    {
      chave: 'nome',
      rotulo: 'Funcionário',
      render: (t) => (
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
          {fotoUrls[t.id] ? (
            <Avatar image={{ src: fotoUrls[t.id] }} size={40} name={t.nome} />
          ) : (
            <Avatar name={t.nome} color="colorful" size={40} />
          )}
          <div style={{ minWidth: 0 }}>
            <div style={{ fontWeight: 600 }}>{t.nome}</div>
            <div style={{ fontSize: 12 }}>
              {t.matricula && `${t.matricula} · `}
              {nomeFuncao(t.funcaoId)} · {tipoVinculoLabel[t.vinculo]}
            </div>
          </div>
        </div>
      ),
    },
    {
      chave: 'situacao',
      rotulo: 'Situação',
      render: (t) => {
        const aso = ultimoAso(asos, t.id);
        return (
          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
            {aso && <StatusChip tom={tomAso[aso.resultadoStatus] ?? 'info'}>{resultadoAsoLabel[aso.resultadoStatus]}</StatusChip>}
            {!t.temBiometria && <StatusChip tom="atencao">Digital pendente</StatusChip>}
          </div>
        );
      },
    },
  ];

  // Sem obra selecionada: tela inicial é a grade de obras com o resumo de vínculo (spec 14/09) —
  // a lista de pessoas só aparece depois do clique numa obra (ver obraSelecionada abaixo).
  if (!obraSelecionadaId) {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        <PageHeader titulo="Funcionários cadastrados" subtitulo="Selecione uma obra para ver os funcionários dela." />

        {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

        {!carregandoLista && obras.length === 0 && (
          <Card>
            <EstadoVazio
              titulo="Nenhuma obra cadastrada ainda."
              descricao="Cadastre uma obra em Obras para depois vincular funcionários a ela."
            />
          </Card>
        )}

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: 16 }}>
          {obras.map((obra) => {
            const contagem = resumoPorObra.get(obra.id) ?? {};
            const total = Object.values(contagem).reduce((soma, n) => soma + n, 0);
            return (
              <Card key={obra.id}>
                <div
                  role="button"
                  tabIndex={0}
                  onClick={() => abrirObra(obra.id)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') abrirObra(obra.id);
                  }}
                  style={{ cursor: 'pointer', display: 'flex', flexDirection: 'column', gap: 8 }}
                >
                  <div style={{ fontWeight: 600 }}>{obra.nome}</div>
                  <div style={{ fontSize: 12 }}>{obra.codigo}</div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 12, marginTop: 4 }}>
                    <PeopleTeam24Regular />
                    <span style={{ fontWeight: 600 }}>{total}</span>
                    <span>{total === 1 ? 'funcionário' : 'funcionários'}</span>
                  </div>
                  <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                    <StatusChip tom="info">CLT: {contagem[TipoVinculo.Clt] ?? 0}</StatusChip>
                    <StatusChip tom="atencao">Terceirizado: {contagem[TipoVinculo.Terceirizado] ?? 0}</StatusChip>
                    <StatusChip tom="ok">Autônomo: {contagem[TipoVinculo.Autonomo] ?? 0}</StatusChip>
                    <StatusChip tom="alerta">Estagiário: {contagem[TipoVinculo.Estagiario] ?? 0}</StatusChip>
                  </div>
                </div>
              </Card>
            );
          })}
        </div>
      </div>
    );
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}
      <PageHeader
        titulo={`Funcionários — ${obraSelecionada?.nome ?? ''}`}
        acoes={
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painelAberto ? fecharPainel() : abrirPainelNovoFuncionario())}
            aria-expanded={painelAberto}
            aria-controls="painel-novo-funcionario"
          >
            {painelAberto ? 'Fechar' : 'Adicionar funcionário'}
          </Button>
        }
        filtros={
          <>
            <Button appearance="subtle" icon={<ArrowLeft24Regular />} onClick={voltarParaObras}>
              Voltar às obras
            </Button>
            <Field label="Buscar por nome ou matrícula">
              <Input contentBefore={<Search24Regular />} value={busca} onChange={(_, d) => setBusca(d.value)} />
            </Field>
          </>
        }
      />

      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      <div id="painel-novo-funcionario">
        <PainelCriacaoInline aberto={painelAberto} titulo="Novo funcionário">
          <FormSection titulo="Dados do funcionário" numero={1} primeira>
            {erroPainel && (
              <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
                {erroPainel}
              </FeedbackInline>
            )}
            <FormGrid>
              <Campo span={6}>
                <Field label="Obra">
                  <Select
                    value={novoTrabalhador.obraId}
                    onChange={(_, d) => setNovoTrabalhador({ ...novoTrabalhador, obraId: d.value })}
                  >
                    <option value="">Selecione</option>
                    {obras.map((obra) => (
                      <option key={obra.id} value={obra.id}>
                        {obra.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Função">
                  <Select
                    value={novoTrabalhador.funcaoId}
                    onChange={(_, d) => setNovoTrabalhador({ ...novoTrabalhador, funcaoId: d.value })}
                  >
                    <option value="">Selecione</option>
                    {funcoes.map((funcao) => (
                      <option key={funcao.id} value={funcao.id}>
                        {funcao.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
              </Campo>
              <Campo span={8}>
                <Field label="Nome">
                  <Input
                    value={novoTrabalhador.nome}
                    onChange={(_, d) => setNovoTrabalhador({ ...novoTrabalhador, nome: d.value })}
                  />
                </Field>
              </Campo>
              <Campo span={4}>
                <Field label="Matrícula">
                  <Input
                    value={novoTrabalhador.matricula ?? ''}
                    onChange={(_, d) => setNovoTrabalhador({ ...novoTrabalhador, matricula: d.value })}
                  />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="CPF (11 dígitos)">
                  <Input
                    value={formatarCpf(novoTrabalhador.cpf)}
                    onChange={(_, d) =>
                      setNovoTrabalhador({ ...novoTrabalhador, cpf: d.value.replace(/\D/g, '').slice(0, 11) })
                    }
                  />
                </Field>
              </Campo>
              <Campo span={3}>
                <Field label="Vínculo">
                  <Select
                    value={novoTrabalhador.vinculo}
                    onChange={(_, d) => setNovoTrabalhador({ ...novoTrabalhador, vinculo: Number(d.value) })}
                  >
                    {Object.entries(tipoVinculoLabel).map(([valor, rotulo]) => (
                      <option key={valor} value={valor}>
                        {rotulo}
                      </option>
                    ))}
                  </Select>
                </Field>
              </Campo>
              <Campo span={3}>
                <Field label="Data de admissão">
                  <CampoData
                    value={novoTrabalhador.dataAdmissao}
                    onChange={(_, d) => setNovoTrabalhador({ ...novoTrabalhador, dataAdmissao: d.value })}
                  />
                </Field>
              </Campo>
            </FormGrid>
            <FormRodape>
              <Button onClick={fecharPainel}>Cancelar</Button>
              <Button appearance="primary" onClick={criar} disabled={carregando}>
                Adicionar funcionário
              </Button>
            </FormRodape>
          </FormSection>
        </PainelCriacaoInline>
      </div>

      <Card>
        <DataTable
          aria-label="Funcionários cadastrados"
          colunas={colunas}
          linhas={trabalhadoresFiltrados}
          chaveLinha={(t) => t.id}
          carregando={carregandoLista}
          aoClicarLinha={(t) => navigate(`/pessoas/${t.id}`)}
          vazio={
            trabalhadores.length === 0
              ? {
                  titulo: 'Nenhum funcionário cadastrado ainda.',
                  descricao: 'Cadastre o primeiro funcionário para começar.',
                  acao: { rotulo: 'Adicionar funcionário', aoClicar: abrirPainelNovoFuncionario },
                }
              : { titulo: 'Nenhum funcionário encontrado.', descricao: 'Tente outro termo de busca.', variante: 'sem-resultado' }
          }
          acoesLinha={(t) => (
            <div style={{ display: 'flex', gap: 4 }}>
              <Button
                appearance="subtle"
                size="small"
                icon={<Fingerprint24Regular />}
                onClick={(evento) => {
                  evento.stopPropagation();
                  setTrabalhadorDigitalAlvo({ id: t.id, nome: t.nome });
                }}
                aria-label="Cadastrar digital"
                title="Cadastrar digital"
              />
              <span onClick={(evento) => evento.stopPropagation()}>
                <SeletorFotoCamera
                  rotulo="Enviar foto"
                  apenasIcone
                  tiposAceitos="image/png,image/jpeg"
                  aoSelecionarArquivo={(arquivo) => enviarFoto(t.id, arquivo)}
                  aoErroValidacao={setErro}
                />
              </span>
              <Button appearance="subtle" size="small" onClick={() => excluir(t.id)} aria-label="Excluir">
                Excluir
              </Button>
            </div>
          )}
        />
      </Card>

      <RequisitosFuncaoDialog
        funcaoId={trabalhadorRequisitosAlvo?.funcaoId ?? null}
        trabalhadorNome={trabalhadorRequisitosAlvo?.nome}
        funcaoNome={trabalhadorRequisitosAlvo ? nomeFuncao(trabalhadorRequisitosAlvo.funcaoId) : undefined}
        aoFechar={() => {
          const alvo = trabalhadorRequisitosAlvo;
          setTrabalhadorRequisitosAlvo(null);
          if (alvo) setTrabalhadorDigitalAlvo({ id: alvo.id, nome: alvo.nome });
        }}
      />

      <CadastroDigitalDialog
        trabalhadorId={trabalhadorDigitalAlvo?.id ?? null}
        trabalhadorNome={trabalhadorDigitalAlvo?.nome}
        aoFechar={() => setTrabalhadorDigitalAlvo(null)}
        aoConcluir={carregar}
      />
    </div>
  );
}
