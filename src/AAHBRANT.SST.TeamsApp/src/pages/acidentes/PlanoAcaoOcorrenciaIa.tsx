import {
  Button,
  Campo,
  CampoData,
  Field,
  FormGrid,
  FormSection,
  Legenda,
  Select,
  Spinner,
  Textarea,
  designTokens,
  tokensUi,
} from '@ui';
import { Add24Regular, ArrowClockwise24Regular, Delete24Regular } from '@fluentui/react-icons';
import {
  metodologiaInvestigacaoLabel,
  prioridadeAcaoLabel,
  tipoAcaoPlanoLabel,
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

interface Props {
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
