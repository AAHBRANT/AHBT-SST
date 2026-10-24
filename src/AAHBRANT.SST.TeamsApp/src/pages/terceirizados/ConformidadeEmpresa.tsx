import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  ArrowUpload24Regular,
  Delete24Regular,
  DocumentPdf24Regular,
  Open24Regular,
} from '@fluentui/react-icons';
import {
  BarraProgresso,
  BotaoAcao,
  Button,
  Card,
  Carregando,
  FeedbackInline,
  Field,
  Input,
  PainelLateral,
  StatusChip,
  Switch,
  Text,
  designTokens,
  tokensUi,
  useConfirmar,
  type Tom,
} from '@ui';
import {
  api,
  type ConformidadeEmpresa,
  type FaixaConformidade,
  type ItemConformidade,
  type NivelConformidade,
  type SituacaoItemConformidade,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { useVisualizadorPdf } from '../../components/useVisualizadorPdf';

// Perfil de adequação da empresa terceirizada (especificação 21/10/2026, etapa 7): rosca com o
// percentual geral, uma barra por nível (documentos da empresa, requisitos das funções, pessoas
// liberadas) e o checklist com o que falta — documentos da empresa são enviados aqui mesmo.

const rotuloFaixa: Record<FaixaConformidade, string> = {
  apta: 'Apta',
  quase: 'Quase apta',
  parcial: 'Parcialmente apta',
  'sem-itens': 'Sem itens exigidos',
};

const tomFaixa: Record<FaixaConformidade, Tom> = {
  apta: 'ok',
  quase: 'atencao',
  parcial: 'alerta',
  'sem-itens': 'neutro',
};

const rotuloSituacao: Record<SituacaoItemConformidade, string> = {
  ok: 'Em dia',
  vence: 'Vence em breve',
  vencido: 'Vencido',
  pendente: 'Pendente',
};

const tomSituacao: Record<SituacaoItemConformidade, Tom> = {
  ok: 'ok',
  vence: 'atencao',
  vencido: 'alerta',
  pendente: 'alerta',
};

// Faixa de um nível isolado, com as mesmas regras do percentual geral.
function tomPercentual(percentual: number | null): Tom {
  if (percentual === null) return 'neutro';
  if (percentual === 100) return 'ok';
  return percentual >= 80 ? 'atencao' : 'alerta';
}

export function ChipFaixa({ faixa, percentual }: { faixa: FaixaConformidade; percentual?: number | null }) {
  return (
    <StatusChip tom={tomFaixa[faixa]}>
      {percentual !== undefined && percentual !== null ? `${percentual}% · ` : ''}
      {rotuloFaixa[faixa]}
    </StatusChip>
  );
}

// Rosca do percentual geral. A cor segue a faixa (dado, não marca — o vinho fica para ação).
export function RoscaConformidade({ percentual, faixa, tamanho = 168 }: { percentual: number | null; faixa: FaixaConformidade; tamanho?: number }) {
  const raio = 70;
  const circunferencia = 2 * Math.PI * raio;
  const preenchido = ((percentual ?? 0) / 100) * circunferencia;
  return (
    <svg
      viewBox="0 0 180 180"
      width={tamanho}
      height={tamanho}
      role="img"
      aria-label={percentual === null ? 'Sem itens exigidos' : `${percentual}% de adequação — ${rotuloFaixa[faixa]}`}
      style={{ flexShrink: 0 }}
    >
      <circle cx="90" cy="90" r={raio} fill="none" stroke={designTokens.colorNeutralLight} strokeWidth="16" />
      {percentual !== null && percentual > 0 && (
        <circle
          cx="90"
          cy="90"
          r={raio}
          fill="none"
          stroke={tokensUi.status[tomFaixa[faixa]].tinta}
          strokeWidth="16"
          strokeLinecap="round"
          strokeDasharray={`${preenchido} ${circunferencia}`}
          transform="rotate(-90 90 90)"
          style={{ transition: `stroke-dasharray ${tokensUi.duracao.entrada} ${tokensUi.curva}` }}
        />
      )}
      <text x="90" y="92" textAnchor="middle" fontSize="38" fontWeight="800" fill="currentColor">
        {percentual === null ? '—' : `${percentual}%`}
      </text>
      <text x="90" y="116" textAnchor="middle" fontSize="13" fill={designTokens.colorNeutralMedium}>
        adequada
      </text>
    </svg>
  );
}

const ordemNiveis: NivelConformidade[] = ['empresa', 'funcoes', 'pessoas'];
const vazioPorNivel: Record<NivelConformidade, string> = {
  empresa: 'Nenhum documento exigido configurado.',
  funcoes: 'As funções das vagas não exigem cursos na matriz de treinamento.',
  pessoas: 'Nenhuma pessoa cadastrada nos contratos vigentes.',
};

interface EnvioDocumento {
  item: ItemConformidade;
  validade: string;
  arquivo: File | null;
}

export function CardConformidade({ empresaId, podeEditar }: { empresaId: string; podeEditar: boolean }) {
  const navigate = useNavigate();
  const sucessoToast = useSucessoToast();
  const { confirmar, dialogElement } = useConfirmar();
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();
  const [dados, setDados] = useState<ConformidadeEmpresa | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [soPendencias, setSoPendencias] = useState(false);
  const [envio, setEnvio] = useState<EnvioDocumento | null>(null);
  const [enviando, setEnviando] = useState(false);
  const [erroEnvio, setErroEnvio] = useState<string | null>(null);
  const entradaArquivo = useRef<HTMLInputElement>(null);

  const carregar = useCallback(async () => {
    try {
      const resultado = await api.terceirizados.empresas.obterConformidade(empresaId);
      setErro(null);
      setDados(resultado);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao calcular a adequação da empresa.');
    }
  }, [empresaId]);

  useEffect(() => {
    let ativo = true;
    api.terceirizados.empresas
      .obterConformidade(empresaId)
      .then((resultado) => {
        if (ativo) setDados(resultado);
      })
      .catch((e: unknown) => {
        if (ativo) setErro(e instanceof Error ? e.message : 'Falha ao calcular a adequação da empresa.');
      });
    return () => {
      ativo = false;
    };
  }, [empresaId]);

  function abrirEnvio(item: ItemConformidade) {
    setErroEnvio(null);
    // Validade em branco: o documento novo traz data nova (repetir a antiga reabriria o vencido).
    setEnvio({ item, validade: '', arquivo: null });
  }

  async function enviar() {
    if (!envio?.item.documentoExigidoId) return;
    if (!envio.arquivo) {
      setErroEnvio('Escolha o arquivo do documento (PDF, JPG ou PNG).');
      return;
    }
    if (envio.item.exigeValidade && !envio.validade) {
      setErroEnvio('Informe a data de validade do documento.');
      return;
    }
    try {
      setEnviando(true);
      setErroEnvio(null);
      await api.terceirizados.empresas.enviarDocumento(empresaId, envio.item.documentoExigidoId, envio.validade || null, envio.arquivo);
      sucessoToast(`${envio.item.titulo} enviado.`);
      setEnvio(null);
      await carregar();
    } catch (e) {
      setErroEnvio(e instanceof Error ? e.message : 'Falha ao enviar o documento.');
    } finally {
      setEnviando(false);
    }
  }

  function verDocumento(item: ItemConformidade) {
    const documentoId = item.documentoId;
    if (!documentoId) return;
    void visualizar({
      titulo: item.titulo,
      nomeArquivo: item.arquivoNome ?? `${item.titulo}.pdf`,
      obter: async () => {
        const arquivo = await api.terceirizados.empresas.baixarDocumento(empresaId, documentoId);
        if (!arquivo) throw new Error('O arquivo deste documento não foi encontrado.');
        return arquivo;
      },
    });
  }

  async function removerDocumento(item: ItemConformidade) {
    if (!item.documentoId) return;
    const ok = await confirmar({
      titulo: `Remover ${item.titulo}?`,
      mensagem: 'O item volta a ficar pendente no checklist da empresa.',
      rotuloConfirmar: 'Remover',
      tom: 'destrutivo',
    });
    if (!ok) return;
    try {
      await api.terceirizados.empresas.removerDocumento(empresaId, item.documentoId);
      sucessoToast('Documento removido.');
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao remover o documento.');
    }
  }

  if (!dados) {
    return (
      <Card titulo="Adequação da empresa">
        {erro ? (
          <FeedbackInline tom="erro" acao={{ rotulo: 'Tentar de novo', aoClicar: () => void carregar() }}>
            {erro}
          </FeedbackInline>
        ) : (
          <Carregando variante="detalhe" linhas={4} />
        )}
      </Card>
    );
  }

  const pendentes = dados.itens.filter((i) => !i.atendido).length;

  return (
    <Card
      titulo="Adequação da empresa"
      acoes={
        <Switch
          checked={soPendencias}
          onChange={(_, d) => setSoPendencias(d.checked)}
          label={`Só o que falta (${pendentes})`}
        />
      }
    >
      {dialogElement}
      {dialogoVisualizador}
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: tokensUi.espaco.xl, alignItems: 'center' }}>
        <RoscaConformidade percentual={dados.percentual} faixa={dados.faixa} />
        <div style={{ flex: '1 1 280px', minWidth: 0, display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.md }}>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: tokensUi.espaco.sm, alignItems: 'center' }}>
            <ChipFaixa faixa={dados.faixa} />
            <Text size={200} style={{ color: designTokens.colorNeutralMedium }}>
              {dados.atendidos} de {dados.exigidos} itens atendidos
            </Text>
          </div>
          {dados.niveis.map((n) => (
            <div key={n.chave} style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', gap: tokensUi.espaco.sm, fontSize: 13.5 }}>
                <span>{n.titulo}</span>
                <span style={{ fontVariantNumeric: 'tabular-nums', color: designTokens.colorNeutralMedium }}>
                  {n.exigidos === 0 ? 'sem itens' : `${n.atendidos} de ${n.exigidos} · ${n.percentual}%`}
                </span>
              </div>
              <BarraProgresso percentual={n.percentual ?? 0} tom={tomPercentual(n.percentual)} aria-label={n.titulo} />
            </div>
          ))}
          <Text size={200} style={{ color: designTokens.colorNeutralMedium }}>
            Faixas: 100% apta · 80 a 99% quase apta · abaixo de 80% parcialmente apta.
          </Text>
        </div>
      </div>

      {ordemNiveis.map((chave) => {
        const nivel = dados.niveis.find((n) => n.chave === chave);
        const itens = dados.itens
          .filter((i) => i.nivel === chave && (!soPendencias || !i.atendido))
          .sort((a, b) => Number(a.atendido) - Number(b.atendido));
        return (
          <section key={chave} style={{ display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.sm, marginTop: tokensUi.espaco.lg }}>
            <Text weight="semibold">{nivel?.titulo}</Text>
            {itens.length === 0 ? (
              <Text size={200} style={{ color: designTokens.colorNeutralMedium }}>
                {nivel && nivel.exigidos > 0 ? 'Tudo em dia neste nível.' : vazioPorNivel[chave]}
              </Text>
            ) : (
              itens.map((item, i) => (
                <div
                  key={`${item.titulo}-${i}`}
                  style={{
                    display: 'flex',
                    flexWrap: 'wrap',
                    alignItems: 'center',
                    gap: tokensUi.espaco.sm,
                    padding: `${tokensUi.espaco.sm} ${tokensUi.espaco.md}`,
                    border: `1px solid ${tokensUi.bordaSuave}`,
                    borderRadius: tokensUi.raio.md,
                  }}
                >
                  <div style={{ flex: '1 1 220px', minWidth: 0 }}>
                    <div style={{ fontWeight: 600, overflowWrap: 'anywhere' }}>{item.titulo}</div>
                    {item.detalhe && (
                      <div style={{ fontSize: 12.5, color: designTokens.colorNeutralMedium, overflowWrap: 'anywhere' }}>{item.detalhe}</div>
                    )}
                  </div>
                  <StatusChip tom={tomSituacao[item.situacao]}>{rotuloSituacao[item.situacao]}</StatusChip>
                  <div style={{ display: 'flex', gap: 4 }}>
                    {item.nivel === 'empresa' && item.documentoId && (
                      <BotaoAcao tom="ver" icon={<DocumentPdf24Regular />} onClick={() => verDocumento(item)} aria-label={`Ver ${item.titulo}`} />
                    )}
                    {item.nivel === 'empresa' && podeEditar && (
                      <BotaoAcao
                        tom="ver"
                        icon={<ArrowUpload24Regular />}
                        onClick={() => abrirEnvio(item)}
                        aria-label={item.documentoId ? `Substituir ${item.titulo}` : `Enviar ${item.titulo}`}
                      />
                    )}
                    {item.nivel === 'empresa' && item.documentoId && podeEditar && (
                      <BotaoAcao
                        tom="excluir"
                        icon={<Delete24Regular />}
                        onClick={() => void removerDocumento(item)}
                        aria-label={`Remover ${item.titulo}`}
                      />
                    )}
                    {item.nivel === 'pessoas' && item.trabalhadorId && (
                      <BotaoAcao
                        tom="ver"
                        icon={<Open24Regular />}
                        onClick={() => navigate(`/pessoas/${item.trabalhadorId}`)}
                        aria-label="Abrir ficha da pessoa"
                      />
                    )}
                  </div>
                </div>
              ))
            )}
          </section>
        );
      })}

      <PainelLateral
        aberto={envio !== null}
        aoFechar={() => setEnvio(null)}
        titulo={envio ? `${envio.item.documentoId ? 'Substituir' : 'Enviar'} ${envio.item.titulo}` : ''}
        subtitulo={dados.razaoSocial}
        rodape={
          <>
            <Button onClick={() => setEnvio(null)}>Cancelar</Button>
            <Button appearance="primary" disabled={enviando} onClick={() => void enviar()}>
              {enviando ? 'Enviando…' : 'Enviar documento'}
            </Button>
          </>
        }
      >
        {envio && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.lg, paddingTop: tokensUi.espaco.sm }}>
            {erroEnvio && (
              <FeedbackInline tom="erro" aoFechar={() => setErroEnvio(null)}>
                {erroEnvio}
              </FeedbackInline>
            )}
            <Field label="Arquivo (PDF, JPG ou PNG, até 20 MB)" required>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: tokensUi.espaco.sm, alignItems: 'center' }}>
                <Button icon={<ArrowUpload24Regular />} onClick={() => entradaArquivo.current?.click()}>
                  Escolher arquivo
                </Button>
                <Text size={200} style={{ overflowWrap: 'anywhere' }}>
                  {envio.arquivo?.name ?? 'Nenhum arquivo escolhido'}
                </Text>
              </div>
            </Field>
            <input
              ref={entradaArquivo}
              type="file"
              accept="application/pdf,.pdf,image/jpeg,image/png"
              hidden
              onChange={(e) => {
                const arquivo = e.target.files?.[0] ?? null;
                e.target.value = '';
                setEnvio((atual) => (atual ? { ...atual, arquivo } : atual));
              }}
            />
            <Field
              label="Validade"
              required={envio.item.exigeValidade}
              hint={envio.item.exigeValidade ? 'Quando vencer, o item reabre e os técnicos das obras da empresa são avisados.' : 'Opcional: este documento não vence.'}
            >
              <Input
                type="date"
                value={envio.validade}
                onChange={(_, d) => setEnvio((atual) => (atual ? { ...atual, validade: d.value } : atual))}
              />
            </Field>
            {envio.item.documentoId && (
              <Text size={200} style={{ color: designTokens.colorNeutralMedium }}>
                O arquivo atual ({envio.item.arquivoNome}) será substituído.
              </Text>
            )}
          </div>
        )}
      </PainelLateral>
    </Card>
  );
}
