import { Button, Field, Input, StatusChip, designTokens, tokensUi } from '@ui';
import type { CSSProperties } from 'react';
import { Delete20Regular } from '@fluentui/react-icons';
import {
  ResultadoPessoaExterna,
  VinculoPessoaExterna,
  resultadoPessoaExternaLabel,
  vinculoPessoaExternaLabel,
} from '../../lib/api';
import { formatarCpf } from '../../lib/cpf';
import { formatarTelefone, idCampoPessoaExterna, type PessoaExternaEmEdicao } from './pessoaExterna';

const estilos = {
  cartao: {
    backgroundColor: designTokens.colorPageBackground,
    border: `1px solid ${designTokens.colorCardBorder}`,
    borderLeft: `4px solid ${designTokens.colorPrimary}`,
    borderRadius: tokensUi.raio.md,
    padding: tokensUi.espaco.lg,
    display: 'grid',
    gridTemplateColumns: 'minmax(0, 1fr)',
    gap: tokensUi.espaco.md,
    minWidth: 0,
  },
  topo: { display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: tokensUi.espaco.sm, fontWeight: 700 },
  tituloTopo: { display: 'inline-flex', alignItems: 'center', gap: tokensUi.espaco.sm },
  grade: { display: 'grid', gap: tokensUi.espaco.md, gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))' },
  linhaInteira: { gridColumn: '1 / -1' },
  segmento: { display: 'flex', flexWrap: 'wrap', gap: tokensUi.espaco.sm },
  // Botões grandes: uso em obra, com luva. 48px de altura mínima, como no mockup aprovado.
  botaoSeg: {
    flex: '1 1 auto',
    minHeight: 48,
    padding: `6px ${tokensUi.espaco.md}`,
    border: `1px solid ${designTokens.colorCardBorder}`,
    borderRadius: tokensUi.raio.sm,
    backgroundColor: designTokens.colorSurface,
    color: 'inherit',
    fontFamily: 'inherit',
    fontSize: 14,
    fontWeight: 600,
    textAlign: 'center',
    cursor: 'pointer',
  },
  botaoSegAtivo: {
    backgroundColor: designTokens.colorPrimary,
    borderColor: designTokens.colorPrimary,
    color: designTokens.colorWhite,
    boxShadow: `0 0 0 2px ${tokensUi.chrome.ativoFundo}`,
  },
  botaoSegObito: {
    backgroundColor: tokensUi.status.alerta.fundo,
    borderColor: tokensUi.status.alerta.tinta,
    color: tokensUi.status.alerta.tinta,
  },
  botaoSegObitoAtivo: {
    backgroundColor: designTokens.colorAlert,
    borderColor: designTokens.colorAlert,
    color: designTokens.colorWhite,
  },
  subrotulo: { display: 'block', fontWeight: 500, fontSize: 11, lineHeight: '13px', opacity: 0.85 },
} as const satisfies Record<string, CSSProperties>;

interface OpcaoSegmento {
  valor: number;
  rotulo: string;
  detalhe?: string;
  perigo?: boolean;
}

// Grupo de botões grandes de escolha única (vínculo e resultado). `id` é o alvo do "Ir" do pop-up de pendências.
function SegmentoBotoes({
  id,
  rotulo,
  opcoes,
  valor,
  aoEscolher,
}: {
  id: string;
  rotulo: string;
  opcoes: OpcaoSegmento[];
  valor: number | null;
  aoEscolher: (valor: number) => void;
}) {
  return (
    <div id={id} role="radiogroup" aria-label={rotulo} style={estilos.segmento} tabIndex={-1}>
      {opcoes.map((o) => {
        const ativo = valor === o.valor;
        return (
          <button
            key={o.valor}
            type="button"
            role="radio"
            aria-checked={ativo}
            style={{
              ...estilos.botaoSeg,
              ...(o.perigo ? estilos.botaoSegObito : {}),
              ...(ativo ? (o.perigo ? estilos.botaoSegObitoAtivo : estilos.botaoSegAtivo) : {}),
            }}
            onClick={() => aoEscolher(o.valor)}
          >
            {o.rotulo}
            {o.detalhe && <span style={estilos.subrotulo}>{o.detalhe}</span>}
          </button>
        );
      })}
    </div>
  );
}

const OPCOES_VINCULO: OpcaoSegmento[] = [
  { valor: VinculoPessoaExterna.Terceiro, rotulo: 'Terceiro / prestador', detalhe: 'de outra empresa' },
  { valor: VinculoPessoaExterna.Visitante, rotulo: vinculoPessoaExternaLabel[VinculoPessoaExterna.Visitante] },
  { valor: VinculoPessoaExterna.Transeunte, rotulo: vinculoPessoaExternaLabel[VinculoPessoaExterna.Transeunte] },
  { valor: VinculoPessoaExterna.Outro, rotulo: vinculoPessoaExternaLabel[VinculoPessoaExterna.Outro] },
];

const OPCOES_RESULTADO: OpcaoSegmento[] = [
  { valor: ResultadoPessoaExterna.SemLesao, rotulo: resultadoPessoaExternaLabel[ResultadoPessoaExterna.SemLesao] },
  { valor: ResultadoPessoaExterna.Lesao, rotulo: resultadoPessoaExternaLabel[ResultadoPessoaExterna.Lesao] },
  { valor: ResultadoPessoaExterna.Obito, rotulo: resultadoPessoaExternaLabel[ResultadoPessoaExterna.Obito], perigo: true },
];

const SUGESTOES_ATENDIMENTO = ['SAMU', 'Bombeiros', 'Primeiros socorros no local', 'Encaminhado ao hospital', 'Sem atendimento'];

interface Props {
  indice: number;
  pessoa: PessoaExternaEmEdicao;
  aoMudar: (patch: Partial<PessoaExternaEmEdicao>) => void;
  aoRemover: () => void;
}

// Cartão em linha de uma pessoa que não é funcionária (opção A do mockup 2026-11-17-ocorrencia-vitima-externa-v1).
// Só o nome e o vínculo são obrigatórios; CPF, idade e telefone são opcionais e dado pessoal (LGPD).
export function CartaoPessoaExterna({ indice, pessoa, aoMudar, aoRemover }: Props) {
  const id = (campo: string) => idCampoPessoaExterna(pessoa.chave, campo);
  const comLesao = pessoa.resultado !== ResultadoPessoaExterna.SemLesao;
  // Qualquer edição tira o selo "IA": o técnico já conferiu aquela pessoa.
  const mudar = (patch: Partial<PessoaExternaEmEdicao>) => aoMudar({ ...patch, sugeridaPelaIa: false });

  return (
    <section style={estilos.cartao} aria-label={`Pessoa externa ${indice + 1}`}>
      <div style={estilos.topo}>
        <span style={estilos.tituloTopo}>
          Pessoa externa {indice + 1}
          {pessoa.sugeridaPelaIa && <StatusChip tom="info">IA</StatusChip>}
        </span>
        <Button appearance="subtle" icon={<Delete20Regular />} onClick={aoRemover} aria-label={`Remover pessoa externa ${indice + 1}`}>
          Remover
        </Button>
      </div>

      <div style={estilos.grade}>
        <div id={id('nome')} style={estilos.linhaInteira}>
          <Field label="Nome" required>
            <Input value={pessoa.nome} maxLength={200} onChange={(_, d) => mudar({ nome: d.value })} />
          </Field>
        </div>

        <div style={estilos.linhaInteira}>
          <Field label="Vínculo com a obra" required>
            {() => (
              <SegmentoBotoes
                id={id('vinculo')}
                rotulo="Vínculo com a obra"
                opcoes={OPCOES_VINCULO}
                valor={pessoa.vinculo}
                aoEscolher={(vinculo) => mudar({ vinculo })}
              />
            )}
          </Field>
        </div>

        {pessoa.vinculo === VinculoPessoaExterna.Terceiro && (
          <div style={estilos.linhaInteira}>
            <Field label="Empresa">
              <Input
                value={pessoa.empresa}
                maxLength={200}
                placeholder="Nome da empresa do prestador"
                onChange={(_, d) => mudar({ empresa: d.value })}
              />
            </Field>
          </div>
        )}

        <div id={id('cpf')}>
          <Field label="CPF (opcional)" hint="Dado pessoal; use só se necessário.">
            <Input
              value={formatarCpf(pessoa.cpf)}
              inputMode="numeric"
              placeholder="000.000.000-00"
              onChange={(_, d) => mudar({ cpf: d.value.replace(/\D/g, '').slice(0, 11) })}
            />
          </Field>
        </div>
        <div id={id('idade')}>
          <Field label="Idade (opcional)">
            <Input
              type="number"
              min={0}
              max={120}
              inputMode="numeric"
              placeholder="00"
              value={pessoa.idade}
              onChange={(_, d) => mudar({ idade: d.value })}
            />
          </Field>
        </div>
        <div id={id('telefone')}>
          <Field label="Telefone/contato (opcional)">
            <Input
              value={formatarTelefone(pessoa.telefone)}
              inputMode="tel"
              placeholder="(00) 00000-0000"
              onChange={(_, d) => mudar({ telefone: d.value.replace(/\D/g, '').slice(0, 11) })}
            />
          </Field>
        </div>

        <div style={estilos.linhaInteira}>
          <Field label="Resultado">
            {() => (
              <SegmentoBotoes
                id={id('resultado')}
                rotulo="Resultado"
                opcoes={OPCOES_RESULTADO}
                valor={pessoa.resultado}
                aoEscolher={(resultado) => mudar({ resultado })}
              />
            )}
          </Field>
        </div>

        {comLesao && (
          <>
            <div>
              <Field label="Lesão / situação">
                <Input
                  value={pessoa.lesao}
                  maxLength={500}
                  placeholder="Descreva em poucas palavras"
                  onChange={(_, d) => mudar({ lesao: d.value })}
                />
              </Field>
            </div>
            <div>
              <Field label="Atendimento">
                <Input
                  value={pessoa.atendimento}
                  maxLength={500}
                  list={id('atendimento-sugestoes')}
                  placeholder="Ex.: SAMU"
                  onChange={(_, d) => mudar({ atendimento: d.value })}
                />
              </Field>
              <datalist id={id('atendimento-sugestoes')}>
                {SUGESTOES_ATENDIMENTO.map((s) => (
                  <option key={s} value={s} />
                ))}
              </datalist>
            </div>
          </>
        )}
      </div>
    </section>
  );
}
