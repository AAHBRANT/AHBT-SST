import {
  Button,
  Campo,
  CampoData,
  Field,
  FormGrid,
  FormSection,
  Input,
  Legenda,
  Select,
  Spinner,
  Textarea,
  designTokens,
  tokensUi,
} from '@ui';
import { Add24Regular, ArrowClockwise24Regular, Delete24Regular, Dismiss16Regular } from '@fluentui/react-icons';
import {
  metodologiaInvestigacaoLabel,
  prioridadeAcaoLabel,
  tipoAcaoPlanoLabel,
  type ParticipanteReuniao,
  type Usuario,
} from '../../lib/api';

export interface AcaoPlanoEmEdicao {
  chave: string;
  tipo: number;
  descricao: string;
  prioridade: number;
  prazo: string; // yyyy-MM-dd
  responsavelUsuarioId: string;
  papelResponsavel: string | null;
  avisoResponsavel: string | null;
  fundamentacao: string | null;
  baseConfirmada: boolean;
  sugeridaPelaIa: boolean;
}

export interface TemaDdsEmEdicao {
  nome: string;
  roteiro: string;
  data: string; // yyyy-MM-dd — próximo dia útil, calculado no servidor
}

export interface ReuniaoEmEdicao {
  data: string; // yyyy-MM-dd
  hora: string; // HH:mm
  duracaoMinutos: number;
  participantes: ParticipanteReuniao[];
  aviso: string | null;
}

interface Props {
  reuniao: ReuniaoEmEdicao | null;
  aoMudarReuniao: (reuniao: ReuniaoEmEdicao) => void;
  temaDds: TemaDdsEmEdicao | null;
  aoMudarTemaDds: (tema: TemaDdsEmEdicao) => void;
  metodologia: number | null;
  causas: string;
  acoes: AcaoPlanoEmEdicao[];
  usuarios: Usuario[];
  gerando: boolean;
  aoMudarMetodologia: (valor: number | null) => void;
  aoMudarCausas: (valor: string) => void;
  aoMudarAcoes: (acoes: AcaoPlanoEmEdicao[]) => void;
  aoGerarNovamente: () => void;
}

const cartao = {
  display: 'grid',
  gap: tokensUi.espaco.sm,
  padding: tokensUi.espaco.md,
  borderRadius: tokensUi.raio.sm,
  border: `1px solid ${designTokens.colorCardBorder}`,
  backgroundColor: designTokens.colorSurface,
} as const;

const cartaoObrigatorio = { ...cartao, border: `1px solid ${designTokens.colorPrimary}` } as const;

const formatarDia = (iso: string) => {
  const [a, m, d] = iso.split('-').map(Number);
  if (!a || !m || !d) return iso;
  return new Date(a, m - 1, d).toLocaleDateString('pt-BR', { weekday: 'short', day: '2-digit', month: '2-digit' });
};

const fundamentacaoEstilo = (confirmada: boolean) =>
  ({
    fontSize: 12,
    lineHeight: '16px',
    padding: '4px 8px',
    borderRadius: tokensUi.raio.sm,
    color: confirmada ? tokensUi.status.info.tinta : tokensUi.status.atencao.tinta,
    backgroundColor: confirmada ? tokensUi.status.info.fundo : tokensUi.status.atencao.fundo,
  }) as const;

let contador = 0;
export const novaChaveAcao = () => `acao-${Date.now()}-${contador++}`;

// Seções 3 (análise preliminar de causas) e 4 (plano de ação) do registro por relato. Tudo que a
// IA sugeriu é editável; nada é gravado antes do "Registrar".
export function PlanoAcaoOcorrenciaIa({
  reuniao,
  aoMudarReuniao,
  temaDds,
  aoMudarTemaDds,
  metodologia,
  causas,
  acoes,
  usuarios,
  gerando,
  aoMudarMetodologia,
  aoMudarCausas,
  aoMudarAcoes,
  aoGerarNovamente,
}: Props) {
  const alterarAcao = (chave: string, patch: Partial<AcaoPlanoEmEdicao>) =>
    aoMudarAcoes(acoes.map((a) => (a.chave === chave ? { ...a, ...patch, avisoResponsavel: patch.responsavelUsuarioId !== undefined ? null : a.avisoResponsavel } : a)));

  const adicionar = () =>
    aoMudarAcoes([
      ...acoes,
      {
        chave: novaChaveAcao(),
        tipo: 1,
        descricao: '',
        prioridade: 3,
        prazo: '',
        responsavelUsuarioId: '',
        papelResponsavel: null,
        avisoResponsavel: null,
        fundamentacao: null,
        baseConfirmada: false,
        sugeridaPelaIa: false,
      },
    ]);

  if (gerando && acoes.length === 0) {
    return (
      <FormSection titulo="Análise de causas e plano de ação" numero={3}>
        <Legenda>
          <Spinner size="extra-tiny" style={{ display: 'inline-flex', verticalAlign: 'middle', marginRight: 6 }} />
          A IA está analisando as causas com base no PGR da obra e montando o plano de ação…
        </Legenda>
      </FormSection>
    );
  }

  return (
    <>
      <FormSection titulo="Análise preliminar de causas" numero={3}>
        <FormGrid>
          <Campo span={4}>
            <Field label="Metodologia">
              <Select
                value={metodologia == null ? '' : String(metodologia)}
                onChange={(_, d) => aoMudarMetodologia(d.value ? Number(d.value) : null)}
              >
                <option value="">Não definida</option>
                {Object.entries(metodologiaInvestigacaoLabel).map(([valor, rotulo]) => (
                  <option key={valor} value={valor}>
                    {rotulo}
                  </option>
                ))}
              </Select>
            </Field>
          </Campo>
          <Campo span={12}>
            <Field
              label="Causas (hipóteses a confirmar na investigação)"
              dica="Análise preliminar feita a partir do relato e do PGR. A investigação formal continua na tela da ocorrência."
            >
              <Textarea value={causas} onChange={(_, d) => aoMudarCausas(d.value)} rows={4} resize="vertical" />
            </Field>
          </Campo>
        </FormGrid>
      </FormSection>

      <FormSection titulo="Plano de ação" numero={4}>
        <div style={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr)', gap: tokensUi.espaco.md }}>
          <Legenda>
            Cada ação já sai com responsável e prazo (regra de prazo por prioridade da empresa). A base indica de onde
            a ação veio: o PGR da obra, um requisito legal cadastrado ou a hierarquia de prevenção da NR-01.
          </Legenda>
          {temaDds && (
            <div style={cartaoObrigatorio}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                <strong style={{ fontSize: 13 }}>DDS do dia seguinte</strong>
                <span style={fundamentacaoEstilo(true)}>Obrigatória</span>
              </div>
              <FormGrid>
                <Campo span={12}>
                  <Field label="Tema" required dica="Tema que o encarregado vai apresentar no DDS. Fica disponível no catálogo de temas.">
                    <Textarea
                      value={temaDds.nome}
                      onChange={(_, d) => aoMudarTemaDds({ ...temaDds, nome: d.value })}
                      rows={1}
                      resize="vertical"
                    />
                  </Field>
                </Campo>
                <Campo span={12}>
                  <Field
                    label="Roteiro para o encarregado"
                    required
                    dica="Texto para ler em voz alta no DDS: o que aconteceu, por que é perigoso e como prevenir. Sem nomes e sem culpados."
                  >
                    <Textarea
                      value={temaDds.roteiro}
                      onChange={(_, d) => aoMudarTemaDds({ ...temaDds, roteiro: d.value })}
                      rows={5}
                      resize="vertical"
                    />
                  </Field>
                </Campo>
              </FormGrid>
              <Legenda>
                Será agendado para o DDS de <strong>{formatarDia(temaDds.data)}</strong> nesta obra e já virá selecionado
                quando o técnico abrir o registro do dia. A ação do plano é concluída quando o DDS for encerrado com este tema.
              </Legenda>
            </div>
          )}
          {reuniao && (
            <div style={cartaoObrigatorio}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                <strong style={{ fontSize: 13 }}>Reunião de análise do acidente (Teams)</strong>
                <span style={fundamentacaoEstilo(true)}>Obrigatória · todo acidente</span>
              </div>
              <FormGrid>
                <Campo span={4}>
                  <Field label="Data" required dica="Próximo dia útil por padrão.">
                    <CampoData value={reuniao.data} onChange={(_, d) => aoMudarReuniao({ ...reuniao, data: d.value })} />
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label="Início" required dica="Sugerido pelo primeiro horário livre na sua agenda do Teams, entre 8h e 17h.">
                    <Input type="time" value={reuniao.hora} onChange={(_, d) => aoMudarReuniao({ ...reuniao, hora: d.value })} />
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label="Duração">
                    <Select
                      value={String(reuniao.duracaoMinutos)}
                      onChange={(_, d) => aoMudarReuniao({ ...reuniao, duracaoMinutos: Number(d.value) })}
                    >
                      {[30, 60, 90, 120].map((m) => (
                        <option key={m} value={m}>
                          {m < 60 ? `${m} minutos` : m === 60 ? '1 hora' : `${m / 60} horas`.replace('.5', ',5')}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </Campo>
                <Campo span={12}>
                  <Field label="Participantes" dica="Convite com link do Teams enviado por e-mail a cada participante.">
                    <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                      {reuniao.participantes.length === 0 && <Legenda>Nenhum participante encontrado para esta obra.</Legenda>}
                      {reuniao.participantes.map((p) => (
                        <Button
                          key={p.usuarioId}
                          size="small"
                          appearance="outline"
                          icon={p.organizador ? undefined : <Dismiss16Regular />}
                          iconPosition="after"
                          disabled={p.organizador}
                          aria-label={p.organizador ? `${p.nome} (organizador)` : `Remover ${p.nome}`}
                          onClick={() =>
                            aoMudarReuniao({ ...reuniao, participantes: reuniao.participantes.filter((x) => x.usuarioId !== p.usuarioId) })
                          }
                        >
                          {p.nome} · {p.papel}
                        </Button>
                      ))}
                    </div>
                  </Field>
                </Campo>
              </FormGrid>
              {reuniao.aviso && <span style={fundamentacaoEstilo(false)}>{reuniao.aviso}</span>}
            </div>
          )}
          {acoes.map((acao, indice) => (
            <div key={acao.chave} style={cartao}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                <strong style={{ fontSize: 13 }}>Ação {indice + 1}</strong>
                <Button
                  size="small"
                  appearance="subtle"
                  icon={<Delete24Regular />}
                  onClick={() => aoMudarAcoes(acoes.filter((a) => a.chave !== acao.chave))}
                >
                  Remover
                </Button>
              </div>
              <FormGrid>
                <Campo span={12}>
                  <Field label="Descrição" required dica="O que deve ser feito, de forma concreta e verificável.">
                    <Textarea
                      value={acao.descricao}
                      onChange={(_, d) => alterarAcao(acao.chave, { descricao: d.value })}
                      rows={2}
                      resize="vertical"
                    />
                  </Field>
                </Campo>
                <Campo span={3}>
                  <Field label="Tipo">
                    <Select value={String(acao.tipo)} onChange={(_, d) => alterarAcao(acao.chave, { tipo: Number(d.value) })}>
                      {Object.entries(tipoAcaoPlanoLabel).map(([valor, rotulo]) => (
                        <option key={valor} value={valor}>
                          {rotulo}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </Campo>
                <Campo span={3}>
                  <Field label="Prioridade">
                    <Select
                      value={String(acao.prioridade)}
                      onChange={(_, d) => alterarAcao(acao.chave, { prioridade: Number(d.value) })}
                    >
                      {Object.entries(prioridadeAcaoLabel).map(([valor, rotulo]) => (
                        <option key={valor} value={valor}>
                          {rotulo}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </Campo>
                <Campo span={3}>
                  <Field label="Prazo" dica="Sugerido pela prioridade, conforme a regra de prazos da empresa. Pode ser ajustado.">
                    <CampoData value={acao.prazo} onChange={(_, d) => alterarAcao(acao.chave, { prazo: d.value })} />
                  </Field>
                </Campo>
                <Campo span={3}>
                  <Field label={acao.papelResponsavel ? `Responsável (${acao.papelResponsavel})` : 'Responsável'}>
                    <Select
                      value={acao.responsavelUsuarioId}
                      onChange={(_, d) => alterarAcao(acao.chave, { responsavelUsuarioId: d.value })}
                    >
                      <option value="">A definir</option>
                      {usuarios.map((u) => (
                        <option key={u.id} value={u.id}>
                          {u.nome}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </Campo>
              </FormGrid>
              {acao.avisoResponsavel && <span style={fundamentacaoEstilo(false)}>{acao.avisoResponsavel}</span>}
              {acao.fundamentacao && (
                <span style={fundamentacaoEstilo(acao.baseConfirmada)}>Base: {acao.fundamentacao}</span>
              )}
            </div>
          ))}
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
            <Button size="small" icon={<Add24Regular />} onClick={adicionar}>
              Adicionar ação
            </Button>
            <Button size="small" icon={<ArrowClockwise24Regular />} onClick={aoGerarNovamente} disabled={gerando}>
              {gerando ? 'Gerando…' : 'Gerar plano novamente'}
            </Button>
            <Legenda>Gerar novamente substitui a análise e as ações sugeridas pelo que estiver no formulário agora.</Legenda>
          </div>
        </div>
      </FormSection>
    </>
  );
}
