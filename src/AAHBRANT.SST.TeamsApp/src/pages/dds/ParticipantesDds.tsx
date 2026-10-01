import { useEffect, useRef, useState } from 'react';
import {
  Button, Card, Checkbox, DataTable, FeedbackInline, Field, Input, Legenda,
  StatusChip, designTokens, type Coluna,
} from '@ui';
import {
  ArrowDownload24Regular, Checkmark24Regular, Dismiss24Regular,
  PeopleTeam24Regular, Search24Regular, Stop24Regular,
} from '@fluentui/react-icons';
import { api, TipoFotoParticipante, type DdsDetalhe, type DdsFuncionario } from '../../lib/api';
import { capturarDigitalLocal, obterDispositivoLocal, type DispositivoLocal } from '../../lib/agenteBiometricoLocal';
import { tocarBipeAssinaturaAceita } from '../../lib/bipeAssinatura';
import { SeletorFotoCamera } from '../../components/SeletorFotoCamera';
import { BotaoBiometriaDigital } from '../../components/assinatura/BotaoBiometriaDigital';
import { ErroFacialDialog } from '../../components/assinatura/ErroFacialDialog';
import { formatarHoraBrasilia } from '../../lib/datas';

const linhaFlex = { display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' as const };
// O backend devolve { erro, motivo } no corpo das rejeições; mostra só o texto do erro.
function mensagemDoErro(e: unknown, fallback: string): string {
  if (!(e instanceof Error)) return fallback;
  const trecho = e.message.match(/\{.*\}$/);
  if (trecho) {
    try {
      const corpo = JSON.parse(trecho[0]) as { erro?: string };
      if (typeof corpo.erro === 'string') return corpo.erro;
    } catch {
      // corpo não era JSON — usa a mensagem inteira
    }
  }
  return e.message || fallback;
}

const normalizar = (texto: string) => texto.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('pt-BR');

interface Props {
  detalhe: DdsDetalhe;
  somenteLeitura: boolean;
  aoAtualizar: (detalhe: DdsDetalhe) => void;
}

export function ParticipantesDds({ detalhe, somenteLeitura, aoAtualizar }: Props) {
  const [compacto, setCompacto] = useState(() => window.matchMedia('(max-width: 640px)').matches);
  const [disponiveis, setDisponiveis] = useState<DdsFuncionario[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [erroCarga, setErroCarga] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [erroFacial, setErroFacial] = useState<string | null>(null);
  const [busca, setBusca] = useState('');
  const [somenteSelecionados, setSomenteSelecionados] = useState(false);
  const [salvando, setSalvando] = useState(false);
  const [selecaoEmGravacao, setSelecaoEmGravacao] = useState<string[] | null>(null);
  const [confirmandoId, setConfirmandoId] = useState<string | null>(null);
  const [baixandoId, setBaixandoId] = useState<string | null>(null);
  const [mensagem, setMensagem] = useState('');
  const [dispositivo, setDispositivo] = useState<DispositivoLocal | null>(null);
  const [verificandoLeitor, setVerificandoLeitor] = useState(false);
  const emOperacao = useRef(false);
  const [filaAberta, setFilaAberta] = useState(false);
  const [resultadoFila, setResultadoFila] = useState<{ tom: 'sucesso' | 'erro' | 'info'; texto: string } | null>(null);
  const filaAtiva = useRef(false);
  const { dds } = detalhe;

  useEffect(() => {
    const media = window.matchMedia('(max-width: 640px)');
    const atualizar = () => setCompacto(media.matches);
    media.addEventListener('change', atualizar);
    return () => media.removeEventListener('change', atualizar);
  }, []);

  async function carregarFuncionarios() {
    setCarregando(true);
    setErroCarga(null);
    try {
      setDisponiveis(await api.dds.listarFuncionarios(dds.id));
    } catch (e) {
      setErroCarga(e instanceof Error ? e.message : 'Falha ao carregar os funcionários da obra.');
    } finally {
      setCarregando(false);
    }
  }

  async function verificarLeitor() {
    setVerificandoLeitor(true);
    try {
      setDispositivo(await obterDispositivoLocal());
    } catch {
      setDispositivo(null);
    } finally {
      setVerificandoLeitor(false);
    }
  }

  useEffect(() => {
    if (!somenteLeitura) {
      void carregarFuncionarios();
      void verificarLeitor();
    } else {
      setCarregando(false);
    }
    // O componente é remontado quando muda o DDS (key no componente pai).
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [dds.id, somenteLeitura]);

  const confirmados = new Map(detalhe.participantes.map((p) => [p.trabalhadorId, p]));
  const pendentes = detalhe.funcionariosSelecionados ?? [];
  const selecionados = new Set([...(selecaoEmGravacao ?? pendentes.map((f) => f.trabalhadorId)), ...confirmados.keys()]);
  const totalPendentes = [...selecionados].filter((id) => !confirmados.has(id)).length;
  const mapaFuncionarios = new Map<string, DdsFuncionario>();
  if (!somenteLeitura) disponiveis.forEach((f) => mapaFuncionarios.set(f.trabalhadorId, f));
  pendentes.forEach((f) => mapaFuncionarios.set(f.trabalhadorId, f));
  detalhe.participantes.forEach((p) => {
    if (!mapaFuncionarios.has(p.trabalhadorId)) {
      mapaFuncionarios.set(p.trabalhadorId, { trabalhadorId: p.trabalhadorId, nome: p.trabalhadorNome, matricula: null });
    }
  });
  const termo = normalizar(busca.trim());
  const funcionarios = [...mapaFuncionarios.values()]
    .filter((f) => (!somenteSelecionados || selecionados.has(f.trabalhadorId))
      && normalizar(`${f.nome} ${f.matricula ?? ''}`).includes(termo))
    .sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR'));
  const ocupado = salvando || confirmandoId !== null;
  const todosSelecionados = disponiveis.length > 0 && disponiveis.every((f) => selecionados.has(f.trabalhadorId));

  async function salvar(ids: string[]) {
    if (emOperacao.current || somenteLeitura) return;
    emOperacao.current = true;
    setSalvando(true);
    setSelecaoEmGravacao(ids);
    setErro(null);
    setMensagem('');
    try {
      aoAtualizar(await api.dds.atualizarFuncionarios(dds.id, ids));
      setMensagem('Seleção salva.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar a seleção. Tente novamente.');
    } finally {
      emOperacao.current = false;
      setSalvando(false);
      setSelecaoEmGravacao(null);
    }
  }

  function alternar(id: string) {
    if (confirmados.has(id)) return;
    const novos = new Set(selecionados);
    if (novos.has(id)) novos.delete(id); else novos.add(id);
    void salvar([...novos]);
  }

  // Fila de digitais: um leitor compartilhado fica aberto e cada funcionário só encosta o dedo — o
  // agente local faz o match 1:N e o backend confere o score. Só a leitura que dá certo toca o bipe;
  // digital não reconhecida ou funcionário fora da obra aparece como erro na tela, sem bipe. Sem
  // dedo no leitor (tempo esgotado) a fila simplesmente rearma a leitura, sem erro.
  const contextoFila = useRef({ detalhe, dispositivo });
  contextoFila.current = { detalhe, dispositivo };

  async function processarLeituraFila(captura: { trabalhadorId: string; score: number }) {
    const { detalhe: atual, dispositivo: leitor } = contextoFila.current;
    if (!leitor) return;
    const jaConfirmado = atual.participantes.find((p) => p.trabalhadorId === captura.trabalhadorId);
    if (jaConfirmado) {
      setResultadoFila({ tom: 'info', texto: `${jaConfirmado.trabalhadorNome} já teve a presença confirmada.` });
      return;
    }
    await api.dds.registrarParticipante(dds.id, captura.trabalhadorId, leitor.dispositivoId, leitor.segredoDispositivo, captura.score);
    const novo = await api.dds.obterDetalhe(dds.id);
    aoAtualizar(novo);
    const nome = novo.participantes.find((p) => p.trabalhadorId === captura.trabalhadorId)?.trabalhadorNome ?? 'Funcionário';
    tocarBipeAssinaturaAceita();
    setResultadoFila({ tom: 'sucesso', texto: `Presença de ${nome} confirmada.` });
  }

  async function lacoFila() {
    while (filaAtiva.current) {
      try {
        const captura = await capturarDigitalLocal();
        if (!filaAtiva.current) break;
        await processarLeituraFila(captura);
        await new Promise((r) => setTimeout(r, 1200));
      } catch (e) {
        if (!filaAtiva.current) break;
        const texto = e instanceof Error ? e.message : '';
        if (/Nenhum dedo detectado|dentro do tempo limite/i.test(texto)) continue;
        if (e instanceof TypeError) {
          setResultadoFila({ tom: 'erro', texto: 'Perdi a conexão com o leitor. Verifique o Agente Biométrico e abra a fila de novo.' });
          fecharFila();
          break;
        }
        setResultadoFila({ tom: 'erro', texto: `Digital não reconhecida: ${mensagemDoErro(e, 'funcionário não localizado. Tente de novo.')}` });
        await new Promise((r) => setTimeout(r, 1500));
      }
    }
  }

  function abrirFila() {
    if (!dispositivo || somenteLeitura || filaAtiva.current) return;
    filaAtiva.current = true;
    setFilaAberta(true);
    setErro(null);
    setMensagem('');
    setResultadoFila({ tom: 'info', texto: 'Fila aberta. Próximo funcionário: encoste o dedo no leitor.' });
    void lacoFila();
  }

  function fecharFila() {
    filaAtiva.current = false;
    setFilaAberta(false);
  }

  useEffect(() => () => { filaAtiva.current = false; }, []);

  // Presença por reconhecimento facial: o operador escolhe o botão facial da linha do funcionário, a
  // foto é identificada no servidor e o rosto precisa ser o do funcionário da linha.
  async function confirmarPresencaFacial(funcionario: DdsFuncionario, foto: File) {
    if (emOperacao.current || somenteLeitura) return;
    emOperacao.current = true;
    setConfirmandoId(funcionario.trabalhadorId);
    setErro(null);
    setErroFacial(null);
    setMensagem('');
    try {
      await api.dds.registrarParticipanteFacial(dds.id, funcionario.trabalhadorId, foto);
      tocarBipeAssinaturaAceita();
      aoAtualizar(await api.dds.obterDetalhe(dds.id));
      setMensagem(`Presença de ${funcionario.nome} confirmada por reconhecimento facial.`);
    } catch (e) {
      setErroFacial(mensagemDoErro(e, 'Falha ao confirmar a presença por reconhecimento facial.'));
    } finally {
      emOperacao.current = false;
      setConfirmandoId(null);
    }
  }

  async function baixarFoto(id: string) {
    const participante = confirmados.get(id);
    if (!participante) return;
    setBaixandoId(id);
    setErro(null);
    try {
      const blob = await api.dds.baixarFotoParticipante(participante.id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `dds-${participante.trabalhadorNome.replace(/\s+/g, '-').toLowerCase()}`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar a foto.');
    } finally {
      setBaixandoId(null);
    }
  }

  function acoesFuncionario(f: DdsFuncionario) {
    const presenca = confirmados.get(f.trabalhadorId);
    if (presenca) return presenca.fotoTipo !== TipoFotoParticipante.Biometria
      ? <Button size="small" icon={<ArrowDownload24Regular />} disabled={baixandoId === f.trabalhadorId}
        onClick={() => void baixarFoto(f.trabalhadorId)}>Baixar foto</Button> : <Checkmark24Regular aria-label="Presença confirmada" />;
    if (somenteLeitura || !selecionados.has(f.trabalhadorId)) return null;
    return <div style={linhaFlex}>
      <SeletorFotoCamera
        aoSelecionarArquivo={(foto) => confirmarPresencaFacial(f, foto)}
        aoErroValidacao={setErroFacial}
        rotulo="Confirmar por facial"
        desabilitado={ocupado}
        tamanho="medium"
        variante="facialAzure"
        modoCamera="user"
        exigirCamera
      />
      <Button size="small" appearance="subtle" icon={<Dismiss24Regular />} disabled={ocupado}
        aria-label={`Remover ${f.nome} da seleção`} onClick={() => alternar(f.trabalhadorId)}>Remover</Button>
    </div>;
  }

  function situacaoFuncionario(f: DdsFuncionario) {
    const presenca = confirmados.get(f.trabalhadorId);
    return presenca ? <div style={{ display: 'grid', gap: 4 }}>
      <StatusChip tom="ok">Presença confirmada</StatusChip>
      <Legenda>{presenca.assinadoEm
        ? `Assinado às ${formatarHoraBrasilia(presenca.assinadoEm)}`
        : 'Assinatura pendente'}</Legenda>
    </div> : selecionados.has(f.trabalhadorId)
      ? <StatusChip tom="atencao">Selecionado · presença pendente</StatusChip>
      : <Legenda>Disponível</Legenda>;
  }

  const colunas: Coluna<DdsFuncionario>[] = [
    ...(!somenteLeitura ? [{
      chave: 'selecao', rotulo: 'Seleção', largura: '72px',
      render: (f: DdsFuncionario) => <Checkbox aria-label={`Selecionar ${f.nome}`}
        checked={selecionados.has(f.trabalhadorId)} disabled={ocupado || confirmados.has(f.trabalhadorId)}
        onChange={() => alternar(f.trabalhadorId)} />,
    }] : []),
    { chave: 'nome', rotulo: 'Funcionário', render: (f) => <div>
      <strong>{f.nome}</strong>
      {f.matricula && <div><Legenda>Matrícula {f.matricula}</Legenda></div>}
    </div> },
    { chave: 'situacao', rotulo: 'Situação', render: situacaoFuncionario },
  ];

  const colunasCompactas: Coluna<DdsFuncionario>[] = [{
    chave: 'funcionario', rotulo: 'Funcionário', render: (f) => <div style={{ display: 'grid', gap: 10 }}>
      <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
        {!somenteLeitura && <Checkbox aria-label={`Selecionar ${f.nome}`}
          checked={selecionados.has(f.trabalhadorId)} disabled={ocupado || confirmados.has(f.trabalhadorId)}
          onChange={() => alternar(f.trabalhadorId)} />}
        <div><strong>{f.nome}</strong>{f.matricula && <div><Legenda>Matrícula {f.matricula}</Legenda></div>}</div>
      </div>
      {situacaoFuncionario(f)}
      {acoesFuncionario(f)}
    </div>,
  }];

  return <Card titulo="Funcionários do DDS" subtitulo={`Obra: ${dds.obraNome}`}>
    <div style={{ display: 'grid', gap: 16 }}>
      <div style={linhaFlex} aria-live="polite">
        <StatusChip tom="info">{selecionados.size} {selecionados.size === 1 ? 'selecionado' : 'selecionados'}</StatusChip>
        <StatusChip tom="ok">{confirmados.size} {confirmados.size === 1 ? 'presença confirmada' : 'presenças confirmadas'}</StatusChip>
        <Legenda>{totalPendentes} com presença pendente</Legenda>
      </div>
      {!somenteLeitura && <div style={linhaFlex}>
        <Button appearance="primary" icon={<PeopleTeam24Regular />}
          disabled={ocupado || carregando || !!erroCarga || disponiveis.length === 0 || todosSelecionados}
          onClick={() => void salvar([...new Set([...selecionados, ...disponiveis.map((f) => f.trabalhadorId)])])}>
          Selecionar todos os funcionários
        </Button>
        <Button icon={<Dismiss24Regular />} disabled={ocupado || totalPendentes === 0}
          onClick={() => void salvar([])}>Limpar seleção</Button>
        {todosSelecionados && <Legenda>Todos os funcionários disponíveis da obra estão selecionados.</Legenda>}
      </div>}
      {!somenteLeitura && <div style={{ display: 'grid', gap: 8 }}>
        <div style={linhaFlex}>
          {filaAberta
            ? <Button appearance="primary" size="large" icon={<Stop24Regular />} onClick={fecharFila}>Fechar fila</Button>
            : <BotaoBiometriaDigital disabled={!dispositivo || carregando} onClick={abrirFila}>Abrir fila</BotaoBiometriaDigital>}
          <Legenda>{filaAberta
            ? 'Leitor aberto: cada funcionário encosta o dedo e a presença é confirmada sozinha.'
            : 'Abra a fila para o pessoal registrar a presença pela digital, um após o outro.'}</Legenda>
        </div>
        {resultadoFila && <div role={resultadoFila.tom === 'erro' ? 'alert' : 'status'}><FeedbackInline tom={resultadoFila.tom}>
          <strong style={{ fontSize: 18 }}>{resultadoFila.texto}</strong>
        </FeedbackInline></div>}
      </div>}
      <div style={{ ...linhaFlex, alignItems: 'end' }}>
        <div style={{ flex: '1 1 260px', minWidth: 0 }}>
          <Field label="Buscar funcionário">
            <Input value={busca} onChange={(_, d) => setBusca(d.value)}
              placeholder="Nome ou matrícula" contentBefore={<Search24Regular />} />
          </Field>
        </div>
        {!somenteLeitura && <Checkbox label="Mostrar somente selecionados"
          checked={somenteSelecionados} onChange={(_, d) => setSomenteSelecionados(d.checked === true)} />}
      </div>
      <Legenda>{somenteLeitura
        ? 'Lista deste DDS. As presenças confirmadas constam nos relatórios.'
        : 'Marque ou desmarque para ajustar a lista. A seleção é salva automaticamente; a presença é confirmada pela fila de digitais ou pelo reconhecimento facial de cada funcionário.'}</Legenda>
      {erroCarga && <FeedbackInline tom="erro" acao={{ rotulo: 'Tentar novamente', aoClicar: () => void carregarFuncionarios() }}>{erroCarga}</FeedbackInline>}
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
      <div role="status" style={{ color: designTokens.colorNeutralMedium, minHeight: 20 }}>
        {salvando ? 'Salvando seleção...' : confirmandoId
          ? `Confirmando a presença de ${mapaFuncionarios.get(confirmandoId)?.nome ?? 'funcionário'}...` : mensagem}
      </div>
      <div style={{ maxHeight: 480, overflow: 'auto' }}>
        <DataTable aria-label="Funcionários do DDS" colunas={compacto ? colunasCompactas : colunas} linhas={funcionarios}
          chaveLinha={(f) => f.trabalhadorId} carregando={carregando} cabecalhoFixo densidade="compacta"
          vazio={{ titulo: busca ? 'Nenhum funcionário encontrado.' : somenteSelecionados
            ? 'Nenhum funcionário selecionado.' : 'Nenhum funcionário disponível.',
            descricao: busca ? 'Tente outro nome ou matrícula.' : somenteSelecionados
              ? 'Desmarque o filtro para escolher funcionários da obra.' : undefined }}
          acoesLinha={compacto ? undefined : acoesFuncionario} />
      </div>
      {!somenteLeitura && totalPendentes > 0 && !dispositivo && <FeedbackInline tom="aviso"
        acao={{ rotulo: verificandoLeitor ? 'Verificando leitor...' : 'Verificar leitor', aoClicar: () => { if (!verificandoLeitor) void verificarLeitor(); } }}>
        Para abrir a fila de digitais, conecte o leitor Futronic e abra o Agente Biométrico. O reconhecimento facial não depende do leitor. Você já pode organizar a lista.
      </FeedbackInline>}
      {!somenteLeitura && confirmados.size > 0 && <Legenda>Funcionários com presença confirmada permanecem na lista ao limpar a seleção.</Legenda>}
    </div>
    <ErroFacialDialog mensagem={erroFacial} aoFechar={() => setErroFacial(null)} />
  </Card>;
}
