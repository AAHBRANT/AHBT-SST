import { useEffect, useMemo, useRef, useState } from 'react';
import {
  Button,
  Campo,
  Card,
  CampoData,
  DataTable,
  FeedbackInline,
  Field,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  PainelCriacaoInline,
  Select,
  SeletorPesquisavel,
  StatusChip,
  nivelVencimento,
  rotuloDeVencimento,
  tomDeVencimento,
  useConfirmar,
  type Coluna,
} from '@ui';
import {
  Add24Regular,
  ArrowDownload24Regular,
  Delete24Regular,
  DocumentPdf24Regular,
  Eye24Regular,
  Image24Regular,
  Warning24Regular,
} from '@fluentui/react-icons';
import {
  api,
  OrigemCertificadoTreinamento,
  origemCertificadoTreinamentoLabel,
  type CertificadoTreinamento,
  type CursoTreinamento,
  type NovoTreinamento,
  type Obra,
  type Trabalhador,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { SeletorFotoCamera } from '../../components/SeletorFotoCamera';
import { salvarBlob, useVisualizadorPdf } from '../../components/useVisualizadorPdf';
import { useSouAdministrador } from '../../lib/UsuarioLogadoContext';

// Sub-aba "Certificados" (pedido do usuário, 22/09). As obras já estavam em andamento quando o
// sistema entrou, então os trabalhadores chegaram com treinamentos feitos antes — muitos por
// terceiros e só em papel. Esta tela existe para lançar essa massa retroativa sem entrar no perfil
// de um trabalhador por vez (que é o que a aba Treinamentos do perfil faz) e para guardar o
// certificado digitalizado, que antes não tinha onde ficar.
//
// O registro criado aqui é o mesmo `Treinamento` de sempre — é por isso que lançar a NR-06 de
// alguém nesta tela destrava na hora a entrega de EPI dele (ver EntregasTab.preencherDadosNr6).

type FiltroSituacao = 'todas' | 'valido' | 'alerta' | 'vencido' | 'sem-anexo';

const FILTRO_SITUACAO_ROTULOS: Record<FiltroSituacao, string> = {
  todas: 'Todas as situações',
  valido: 'Válidos',
  alerta: 'Vencendo em até 30 dias',
  vencido: 'Vencidos',
  'sem-anexo': 'Pendentes de anexo',
};

const TIPOS_ACEITOS = 'application/pdf,image/jpeg,image/png';
const TAMANHO_MAXIMO_MB = 10;

function certificadoVazio(): NovoTreinamento {
  return {
    trabalhadorId: '',
    cursoTreinamentoId: '',
    dataRealizacao: '',
    dataValidade: '',
    cargaHorariaRealizada: 0,
    instituicaoInstrutor: '',
    numeroCertificado: '',
    local: '',
    instrutorRegistroProfissional: '',
    origemCertificado: OrigemCertificadoTreinamento.Externo,
  };
}

// Soma meses preservando o fim de mês (31/01 + 1 mês = 28/02, não 03/03). Usado para propor a
// validade a partir da data de realização e da validade em meses do curso — o técnico ainda pode
// corrigir à mão, porque certificado antigo às vezes traz validade própria.
function somarMeses(dataIso: string, meses: number): string {
  if (!dataIso || !meses) return '';
  const [ano, mes, dia] = dataIso.slice(0, 10).split('-').map(Number);
  if (!ano || !mes || !dia) return '';
  const alvo = new Date(ano, mes - 1 + meses, 1);
  const ultimoDiaDoMes = new Date(alvo.getFullYear(), alvo.getMonth() + 1, 0).getDate();
  alvo.setDate(Math.min(dia, ultimoDiaDoMes));
  const mm = String(alvo.getMonth() + 1).padStart(2, '0');
  const dd = String(alvo.getDate()).padStart(2, '0');
  return `${alvo.getFullYear()}-${mm}-${dd}`;
}

function formatarData(valor?: string | null): string {
  if (!valor) return '—';
  const [ano, mes, dia] = valor.slice(0, 10).split('-');
  return dia && mes && ano ? `${dia}/${mes}/${ano}` : valor.slice(0, 10);
}

export function CertificadosTab() {
  const souAdministrador = useSouAdministrador();
  const [certificados, setCertificados] = useState<CertificadoTreinamento[]>([]);
  const [cursos, setCursos] = useState<CursoTreinamento[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [erro, setErro] = useState<string | null>(null);
  const sucessoToast = useSucessoToast();
  const { confirmar, dialogElement } = useConfirmar();

  const [filtroObraId, setFiltroObraId] = useState('');
  const [filtroCursoId, setFiltroCursoId] = useState('');
  const [filtroSituacao, setFiltroSituacao] = useState<FiltroSituacao>('todas');
  const [busca, setBusca] = useState('');

  const [painelAberto, setPainelAberto] = useState(false);
  const [novo, setNovo] = useState<NovoTreinamento>(certificadoVazio);
  const [arquivo, setArquivo] = useState<File | null>(null);
  const [salvando, setSalvando] = useState(false);
  const [erroPainel, setErroPainel] = useState<string | null>(null);

  // Um único input de arquivo para o botão "anexar" de todas as linhas, e não um seletor por linha:
  // a lista chega a centenas de certificados, e montar um componente de upload (com diálogo de
  // câmera) por linha deixaria a tela pesada à toa. `anexandoParaId` diz a qual certificado o
  // arquivo escolhido pertence.
  const inputAnexoListaRef = useRef<HTMLInputElement>(null);
  const [anexandoParaId, setAnexandoParaId] = useState<string | null>(null);

  const { visualizar: abrirVisualizador, dialogoVisualizador } = useVisualizadorPdf();
  const [ocupadoId, setOcupadoId] = useState<string | null>(null);

  async function carregar() {
    try {
      setErro(null);
      setCarregandoLista(true);
      const [lista, listaCursos, listaTrabalhadores, listaObras] = await Promise.all([
        api.treinamentos.listarCertificados(filtroObraId ? { obraId: filtroObraId } : undefined),
        api.cursosTreinamento.listar(),
        api.trabalhadores.listar(filtroObraId || undefined),
        api.obras.listar(),
      ]);
      setCertificados(lista);
      setCursos(listaCursos);
      setTrabalhadores(listaTrabalhadores);
      setObras(listaObras);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar os certificados.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filtroObraId]);

  const listaFiltrada = useMemo(() => {
    const termo = busca.trim().toLowerCase();
    return certificados.filter((c) => {
      if (filtroCursoId && c.cursoTreinamentoId !== filtroCursoId) return false;
      if (filtroSituacao === 'sem-anexo' && c.temArquivo) return false;
      if (filtroSituacao !== 'todas' && filtroSituacao !== 'sem-anexo') {
        if (nivelVencimento(c.dataValidade) !== filtroSituacao) return false;
      }
      if (!termo) return true;
      return (
        c.trabalhadorNome.toLowerCase().includes(termo) ||
        c.cursoNome.toLowerCase().includes(termo) ||
        (c.numeroCertificado ?? '').toLowerCase().includes(termo) ||
        (c.instituicaoInstrutor ?? '').toLowerCase().includes(termo)
      );
    });
  }, [certificados, filtroCursoId, filtroSituacao, busca]);

  // Externo sem anexo é pendência de verdade: é o certificado de terceiro que o sistema não emite e
  // ninguém consegue apresentar numa fiscalização. Fica no topo da tela como contador.
  const pendentesDeAnexo = useMemo(
    () => certificados.filter((c) => c.origemCertificado === OrigemCertificadoTreinamento.Externo && !c.temArquivo).length,
    [certificados],
  );

  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
    setNovo(certificadoVazio());
    setArquivo(null);
  }

  // Curso escolhido propõe carga horária e validade — no lançamento retroativo em massa é o que
  // mais poupa digitação, e o técnico corrige quando o certificado antigo diverge.
  function selecionarCurso(cursoId: string) {
    const curso = cursos.find((c) => c.id === cursoId);
    setNovo((atual) => ({
      ...atual,
      cursoTreinamentoId: cursoId,
      cargaHorariaRealizada: atual.cargaHorariaRealizada || (curso?.cargaHorariaMinima ?? 0),
      dataValidade:
        atual.dataValidade ||
        (atual.dataRealizacao && curso ? somarMeses(atual.dataRealizacao, curso.validadeEmMeses) : ''),
    }));
  }

  function selecionarDataRealizacao(valor: string) {
    const curso = cursos.find((c) => c.id === novo.cursoTreinamentoId);
    setNovo((atual) => ({
      ...atual,
      dataRealizacao: valor,
      dataValidade: curso && valor ? somarMeses(valor, curso.validadeEmMeses) : atual.dataValidade,
    }));
  }

  async function salvar() {
    if (!novo.trabalhadorId || !novo.cursoTreinamentoId || !novo.dataRealizacao || !novo.dataValidade) {
      setErroPainel('Preencha funcionário, curso, data de realização e validade.');
      return;
    }
    if (novo.origemCertificado === OrigemCertificadoTreinamento.Externo && !arquivo) {
      setErroPainel(
        'Certificado de instituição externa exige o arquivo anexado — a AAHBRANT não emite certificado de treinamento que não ministrou.',
      );
      return;
    }
    try {
      setSalvando(true);
      setErroPainel(null);
      const { id } = await api.treinamentos.criar(novo);
      if (arquivo) {
        try {
          await api.treinamentos.anexarArquivoCertificado(id, arquivo);
        } catch (e) {
          // O registro já está salvo: avisa que só o anexo falhou, para o técnico reenviar pela
          // lista em vez de digitar tudo de novo.
          await carregar();
          setErroPainel(
            `Certificado cadastrado, mas o arquivo não subiu (${
              e instanceof Error ? e.message : 'falha no envio'
            }). Use o botão de anexo na lista para reenviar.`,
          );
          return;
        }
      }
      await carregar();
      sucessoToast('Certificado cadastrado com sucesso.');
      fecharPainel();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao cadastrar o certificado.');
    } finally {
      setSalvando(false);
    }
  }

  // Externo: o documento válido é o arquivo original digitalizado. AAHBRANT: emite o modelo
  // próprio como sempre — a API recusa emitir o modelo para certificado externo.
  function obterArquivo(certificado: CertificadoTreinamento) {
    return certificado.origemCertificado === OrigemCertificadoTreinamento.Externo || certificado.temArquivo
      ? api.treinamentos.baixarArquivoCertificado(certificado.id)
      : api.treinamentos.baixarCertificado(certificado.id);
  }

  function nomeArquivo(certificado: CertificadoTreinamento) {
    return (
      certificado.nomeArquivo ??
      `certificado-${certificado.trabalhadorNome.replace(/\s+/g, '-').toLowerCase()}-${certificado.id}.pdf`
    );
  }

  // Externo sem arquivo anexado não tem o que mostrar (a API recusa emitir o modelo para ele).
  function podeVisualizar(certificado: CertificadoTreinamento) {
    return certificado.temArquivo || certificado.origemCertificado !== OrigemCertificadoTreinamento.Externo;
  }

  function visualizar(certificado: CertificadoTreinamento) {
    if (!podeVisualizar(certificado)) return;
    void abrirVisualizador({
      titulo: `${certificado.cursoNome} — ${certificado.trabalhadorNome}`,
      nomeArquivo: nomeArquivo(certificado),
      obter: () => obterArquivo(certificado),
    });
  }

  async function baixar(certificado: CertificadoTreinamento) {
    try {
      setOcupadoId(certificado.id);
      salvarBlob(await obterArquivo(certificado), nomeArquivo(certificado));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar o certificado.');
    } finally {
      setOcupadoId(null);
    }
  }

  function pedirArquivoPara(certificadoId: string) {
    setAnexandoParaId(certificadoId);
    if (inputAnexoListaRef.current) inputAnexoListaRef.current.value = '';
    inputAnexoListaRef.current?.click();
  }

  async function reenviarAnexo(certificado: CertificadoTreinamento, escolhido: File) {
    try {
      setOcupadoId(certificado.id);
      await api.treinamentos.anexarArquivoCertificado(certificado.id, escolhido);
      await carregar();
      sucessoToast('Arquivo do certificado atualizado.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao enviar o arquivo do certificado.');
    } finally {
      setOcupadoId(null);
    }
  }

  async function excluir(certificado: CertificadoTreinamento) {
    const confirmado = await confirmar(
      `Excluir o certificado de ${certificado.cursoNome} de ${certificado.trabalhadorNome}? Essa ação não pode ser desfeita.`,
    );
    if (!confirmado) return;
    try {
      await api.treinamentos.excluir(certificado.id);
      await carregar();
      sucessoToast('Certificado excluído.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir o certificado.');
    }
  }

  const colunas: Coluna<CertificadoTreinamento>[] = [
    {
      chave: 'trabalhador',
      rotulo: 'Funcionário',
      render: (c) => (
        <div style={{ display: 'flex', flexDirection: 'column' }}>
          <span>{c.trabalhadorNome}</span>
          {c.funcaoNome && <span style={{ opacity: 0.7, fontSize: 12 }}>{c.funcaoNome}</span>}
        </div>
      ),
    },
    {
      chave: 'curso',
      rotulo: 'Curso',
      render: (c) => (
        <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
          <span>{c.cursoNome}</span>
          {c.atendeNr6 && <StatusChip tom="info">NR-06</StatusChip>}
        </div>
      ),
    },
    { chave: 'realizacao', rotulo: 'Realização', render: (c) => formatarData(c.dataRealizacao) },
    {
      chave: 'validade',
      rotulo: 'Validade',
      render: (c) => {
        const nivel = nivelVencimento(c.dataValidade);
        return (
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <span>{formatarData(c.dataValidade)}</span>
            {nivel && <StatusChip tom={tomDeVencimento(nivel)}>{rotuloDeVencimento(nivel)}</StatusChip>}
          </div>
        );
      },
    },
    {
      chave: 'origem',
      rotulo: 'Origem',
      render: (c) => (
        <StatusChip tom={c.origemCertificado === OrigemCertificadoTreinamento.Externo ? 'neutro' : 'ok'}>
          {origemCertificadoTreinamentoLabel[c.origemCertificado]}
        </StatusChip>
      ),
    },
    {
      chave: 'anexo',
      rotulo: 'Arquivo',
      render: (c) => {
        if (!c.temArquivo) {
          return c.origemCertificado === OrigemCertificadoTreinamento.Externo ? (
            <StatusChip tom="alerta">Pendente</StatusChip>
          ) : (
            <span style={{ opacity: 0.6 }}>—</span>
          );
        }
        return (
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            {c.contentTypeArquivo === 'application/pdf' ? <DocumentPdf24Regular /> : <Image24Regular />}
            <span style={{ fontSize: 12, opacity: 0.8 }}>{c.nomeArquivo}</span>
          </div>
        );
      },
    },
  ];

  const opcoesCursos = cursos.map((c) => ({ id: c.id, rotulo: c.nome }));
  const opcoesTrabalhadores = trabalhadores.map((t) => ({ id: t.id, rotulo: t.nome }));
  const cursoSelecionado = cursos.find((c) => c.id === novo.cursoTreinamentoId);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}

      <div id="painel-novo-certificado">
        <PainelCriacaoInline aberto={painelAberto} titulo="Lançar certificado">
          <FormSection titulo="Dados do certificado" numero={1} primeira>
            {erroPainel && (
              <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
                {erroPainel}
              </FeedbackInline>
            )}
            <FormGrid>
              <Campo span={6}>
                <Field label="Funcionário" required>
                  <SeletorPesquisavel
                    placeholder="Selecione o funcionário"
                    opcaoVazia="Selecione o funcionário"
                    opcoes={opcoesTrabalhadores}
                    valor={novo.trabalhadorId}
                    aoMudar={(id) => setNovo({ ...novo, trabalhadorId: id })}
                  />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Curso" required>
                  <SeletorPesquisavel
                    placeholder="Selecione o curso"
                    opcaoVazia="Selecione o curso"
                    opcoes={opcoesCursos}
                    valor={novo.cursoTreinamentoId}
                    aoMudar={selecionarCurso}
                  />
                </Field>
              </Campo>
              <Campo span={12}>
                <Field
                  label="Origem do treinamento"
                  required
                  hint={
                    novo.origemCertificado === OrigemCertificadoTreinamento.Externo
                      ? 'Curso ministrado por terceiro. O sistema não emite certificado no modelo AAHBRANT — o documento válido é o arquivo anexado, que passa a ser obrigatório.'
                      : 'Curso ministrado pela AAHBRANT. O sistema continua emitindo o certificado no modelo próprio; o anexo é só um comprovante opcional.'
                  }
                >
                  <Select
                    value={String(novo.origemCertificado ?? OrigemCertificadoTreinamento.Externo)}
                    onChange={(_, d) =>
                      setNovo({ ...novo, origemCertificado: Number(d.value) as OrigemCertificadoTreinamento })
                    }
                  >
                    <option value={String(OrigemCertificadoTreinamento.Externo)}>
                      Externo — outra instituição (SENAI, empresa anterior, curso avulso)
                    </option>
                    <option value={String(OrigemCertificadoTreinamento.Aahbrant)}>
                      AAHBRANT — ministrado pela empresa
                    </option>
                  </Select>
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Data de realização" required>
                  <CampoData value={novo.dataRealizacao} onChange={(_, d) => selecionarDataRealizacao(d.value)} />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field
                  label="Validade"
                  required
                  hint={
                    cursoSelecionado
                      ? `Proposta a partir da validade do curso (${cursoSelecionado.validadeEmMeses} meses). Corrija se o certificado trouxer outra data.`
                      : undefined
                  }
                >
                  <CampoData
                    value={novo.dataValidade}
                    onChange={(_, d) => setNovo({ ...novo, dataValidade: d.value })}
                  />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Carga horária realizada (h)">
                  <Input
                    type="number"
                    value={String(novo.cargaHorariaRealizada)}
                    onChange={(_, d) => setNovo({ ...novo, cargaHorariaRealizada: Number(d.value) })}
                  />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field
                  label="Número do certificado"
                  hint="Alimenta o nº da lista de presença na ficha de EPI quando o curso é de NR-06. Deixe vazio se o certificado não tiver numeração."
                >
                  <Input
                    value={novo.numeroCertificado ?? ''}
                    onChange={(_, d) => setNovo({ ...novo, numeroCertificado: d.value })}
                  />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Instituição / Instrutor">
                  <Input
                    value={novo.instituicaoInstrutor ?? ''}
                    onChange={(_, d) => setNovo({ ...novo, instituicaoInstrutor: d.value })}
                  />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Registro profissional do instrutor (CREA/MTE)">
                  <Input
                    value={novo.instrutorRegistroProfissional ?? ''}
                    onChange={(_, d) => setNovo({ ...novo, instrutorRegistroProfissional: d.value })}
                  />
                </Field>
              </Campo>
              <Campo span={12}>
                <Field label="Local / Instalações (opcional — sem preencher, usa a Obra)">
                  <Input value={novo.local ?? ''} onChange={(_, d) => setNovo({ ...novo, local: d.value })} />
                </Field>
              </Campo>
            </FormGrid>
          </FormSection>

          <FormSection titulo="Arquivo do certificado" numero={2}>
            <FormGrid>
              <Campo span={12}>
                <Field
                  label="Certificado digitalizado (PDF, JPEG ou PNG — até 10 MB)"
                  required={novo.origemCertificado === OrigemCertificadoTreinamento.Externo}
                  hint="Suba o PDF original ou tire uma foto do certificado impresso."
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
                    {/* Dois caminhos, porque o certificado retroativo chega das duas formas: o PDF
                        original que o RH/instituição mandou por e-mail, ou o papel na mão do técnico
                        no canteiro. O de foto comprime a imagem antes de enviar (useCapturaFoto). */}
                    <SeletorFotoCamera
                      rotulo="Tirar foto do certificado"
                      aoSelecionarArquivo={(f) => setArquivo(f)}
                      aoErroValidacao={(m) => setErroPainel(m)}
                      tamanhoMaximoMb={TAMANHO_MAXIMO_MB}
                      tiposAceitos="image/jpeg,image/png"
                      tamanho="medium"
                    />
                    <SeletorFotoCamera
                      rotulo="Escolher arquivo (PDF ou imagem)"
                      aoSelecionarArquivo={(f) => setArquivo(f)}
                      aoErroValidacao={(m) => setErroPainel(m)}
                      tamanhoMaximoMb={TAMANHO_MAXIMO_MB}
                      tiposAceitos={TIPOS_ACEITOS}
                      permitirCamera={false}
                      tamanho="medium"
                    />
                    {arquivo && (
                      <StatusChip tom="ok">
                        {arquivo.name} ({(arquivo.size / 1024).toFixed(0)} KB)
                      </StatusChip>
                    )}
                  </div>
                </Field>
              </Campo>
            </FormGrid>
            <FormRodape>
              <Button onClick={fecharPainel}>Cancelar</Button>
              <Button appearance="primary" onClick={salvar} disabled={salvando}>
                Lançar certificado
              </Button>
            </FormRodape>
          </FormSection>
        </PainelCriacaoInline>
      </div>

      {dialogoVisualizador}

      <Card
        titulo="Certificados de treinamento"
        acoes={
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painelAberto ? fecharPainel() : setPainelAberto(true))}
            aria-expanded={painelAberto}
            aria-controls="painel-novo-certificado"
          >
            {painelAberto ? 'Fechar' : 'Lançar certificado'}
          </Button>
        }
      >
        {erro && (
          <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
            {erro}
          </FeedbackInline>
        )}

        {pendentesDeAnexo > 0 && (
          <FeedbackInline tom="aviso">
            {pendentesDeAnexo} certificado(s) de instituição externa sem o arquivo anexado. Sem o documento original,
            não há o que apresentar numa fiscalização — use o filtro "Pendentes de anexo" para regularizar.
          </FeedbackInline>
        )}

        <FormGrid>
          <Campo span={3}>
            <Field label="Obra">
              <Select value={filtroObraId} onChange={(_, d) => setFiltroObraId(d.value)}>
                <option value="">Todas as obras</option>
                {obras.map((o) => (
                  <option key={o.id} value={o.id}>
                    {o.nome}
                  </option>
                ))}
              </Select>
            </Field>
          </Campo>
          <Campo span={3}>
            <Field label="Curso">
              <Select value={filtroCursoId} onChange={(_, d) => setFiltroCursoId(d.value)}>
                <option value="">Todos os cursos</option>
                {cursos.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.nome}
                  </option>
                ))}
              </Select>
            </Field>
          </Campo>
          <Campo span={3}>
            <Field label="Situação">
              <Select value={filtroSituacao} onChange={(_, d) => setFiltroSituacao(d.value as FiltroSituacao)}>
                {Object.entries(FILTRO_SITUACAO_ROTULOS).map(([valor, rotulo]) => (
                  <option key={valor} value={valor}>
                    {rotulo}
                  </option>
                ))}
              </Select>
            </Field>
          </Campo>
          <Campo span={3}>
            <Field label="Buscar">
              <Input
                placeholder="Funcionário, curso ou nº"
                value={busca}
                onChange={(_, d) => setBusca(d.value)}
              />
            </Field>
          </Campo>
        </FormGrid>

        <DataTable
          aria-label="Certificados de treinamento"
          colunas={colunas}
          linhas={listaFiltrada}
          chaveLinha={(c) => c.id}
          carregando={carregandoLista}
          aoClicarLinha={(c) => visualizar(c)}
          vazio={{
            titulo: 'Nenhum certificado encontrado',
            descricao: 'Lance os certificados dos funcionários que já estavam na obra antes do sistema.',
            acao: { rotulo: 'Lançar certificado', aoClicar: () => setPainelAberto(true) },
          }}
          acoesLinha={(c) => (
            <div style={{ display: 'flex', gap: 4 }}>
              {podeVisualizar(c) && (
                <Button
                  appearance="subtle"
                  size="small"
                  icon={<Eye24Regular />}
                  onClick={(evento) => {
                    evento.stopPropagation();
                    visualizar(c);
                  }}
                  disabled={ocupadoId === c.id}
                  aria-label="Visualizar certificado"
                  title="Visualizar certificado"
                />
              )}
              {!c.temArquivo && (
                <Button
                  appearance="subtle"
                  size="small"
                  icon={<Warning24Regular />}
                  onClick={(evento) => {
                    evento.stopPropagation();
                    pedirArquivoPara(c.id);
                  }}
                  disabled={ocupadoId === c.id}
                  aria-label="Anexar arquivo do certificado"
                  title="Anexar arquivo do certificado"
                />
              )}
              <Button
                appearance="subtle"
                size="small"
                icon={<ArrowDownload24Regular />}
                onClick={(evento) => {
                  evento.stopPropagation();
                  baixar(c);
                }}
                disabled={ocupadoId === c.id}
                aria-label="Baixar certificado"
                title={
                  c.origemCertificado === OrigemCertificadoTreinamento.Externo
                    ? 'Baixar o certificado original anexado'
                    : 'Baixar o certificado no modelo AAHBRANT'
                }
              />
              {/* Exclusão é privilégio de Administrador (o servidor recusa os demais) — ver PoliticasAutorizacao. */}
              {souAdministrador && (
                <Button
                  appearance="subtle"
                  size="small"
                  icon={<Delete24Regular />}
                  onClick={(evento) => {
                    evento.stopPropagation();
                    excluir(c);
                  }}
                  aria-label="Excluir certificado"
                />
              )}
            </div>
          )}
        />
      </Card>

      <input
        ref={inputAnexoListaRef}
        type="file"
        accept={TIPOS_ACEITOS}
        style={{ display: 'none' }}
        onChange={(e) => {
          const escolhido = e.target.files?.[0];
          const certificado = certificados.find((c) => c.id === anexandoParaId);
          if (escolhido && certificado) {
            if (escolhido.size > TAMANHO_MAXIMO_MB * 1024 * 1024) {
              setErro(`O arquivo deve ter no máximo ${TAMANHO_MAXIMO_MB} MB.`);
            } else {
              reenviarAnexo(certificado, escolhido);
            }
          }
          setAnexandoParaId(null);
        }}
      />
    </div>
  );
}
