import { Button, Checkbox, FeedbackInline, Field, Legenda, StatusChip, designTokens, tokensUi } from '@ui';
import type { CSSProperties } from 'react';
import { Add20Regular } from '@fluentui/react-icons';
import { MAXIMO_PESSOAS_EXTERNAS, ResultadoPessoaExterna, type ProvidenciaObito } from '../../lib/api';
import { CartaoPessoaExterna } from './CartaoPessoaExterna';
import { novaPessoaExterna, type PessoaExternaEmEdicao } from './pessoaExterna';

const estilos = {
  lista: { display: 'grid', gridTemplateColumns: 'minmax(0, 1fr)', gap: tokensUi.espaco.md, minWidth: 0 },
  adicionar: {
    width: '100%',
    minHeight: 48,
    fontWeight: 700,
    color: designTokens.colorPrimary,
    border: `1px dashed ${designTokens.colorPrimary}`,
  },
  lembrete: {
    backgroundColor: tokensUi.status.alerta.fundo,
    border: `1px solid ${tokensUi.status.alerta.tinta}`,
    borderRadius: tokensUi.raio.md,
    padding: tokensUi.espaco.lg,
    display: 'grid',
    gridTemplateColumns: 'minmax(0, 1fr)',
    gap: tokensUi.espaco.sm,
  },
  lembreteTitulo: { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: tokensUi.espaco.sm, fontWeight: 700 },
  itens: { listStyle: 'none', margin: 0, padding: 0, display: 'grid', gap: tokensUi.espaco.xs },
  item: { display: 'flex', alignItems: 'flex-start', gap: tokensUi.espaco.md, minHeight: 36, paddingTop: tokensUi.espaco.xs },
  caixaVazia: {
    flex: '0 0 22px',
    height: 22,
    marginTop: 2,
    border: `2px solid ${designTokens.colorNeutralMedium}`,
    borderRadius: 5,
  },
  concluida: { textDecorationLine: 'line-through', opacity: 0.75 },
} as const satisfies Record<string, CSSProperties>;

// Providências quando há óbito. Sem `itens`, é só o lembrete do formulário de registro (o checklist com
// marcação existe depois, na tela da ocorrência). Com `itens`, é o checklist gravado no servidor.
// Não define prazos legais: o que depende de norma ou prazo diz "confirmar com o jurídico".
const ITENS_LEMBRETE = [
  'Acionar a diretoria e o jurídico',
  'Preservar o local da ocorrência',
  'Comunicar as autoridades competentes, conforme orientação do jurídico',
];

export function LembreteProvidenciasObito({
  itens,
  aoAlternar,
  desabilitado,
}: {
  itens?: ProvidenciaObito[];
  aoAlternar?: (codigo: string, concluida: boolean) => void;
  desabilitado?: boolean;
}) {
  return (
    <div style={estilos.lembrete} role="note" aria-label="Providências em caso de óbito">
      <div style={estilos.lembreteTitulo}>
        <span>Óbito registrado: providências</span>
        <StatusChip tom="atencao">confirmar com o jurídico</StatusChip>
      </div>
      <ul style={estilos.itens}>
        {itens
          ? itens.map((i) => (
              <li key={i.codigo} style={estilos.item}>
                <Checkbox
                  checked={i.concluida}
                  disabled={desabilitado || !aoAlternar}
                  onChange={(_, d) => aoAlternar?.(i.codigo, !!d.checked)}
                  label={<span style={i.concluida ? estilos.concluida : undefined}>{i.descricao}</span>}
                />
              </li>
            ))
          : ITENS_LEMBRETE.map((t) => (
              <li key={t} style={estilos.item}>
                <i style={estilos.caixaVazia} aria-hidden />
                <span>{t}</span>
              </li>
            ))}
      </ul>
      <Legenda>Este lembrete não define prazos legais. Prazos e órgãos a comunicar devem ser confirmados com o jurídico.</Legenda>
    </div>
  );
}

interface Props {
  pessoas: PessoaExternaEmEdicao[];
  aoMudarLista: (atualizar: (atuais: PessoaExternaEmEdicao[]) => PessoaExternaEmEdicao[]) => void;
  // Tipo "Ocorrência com pessoa externa": mostra o aviso fixo sobre CAT e NBR 14280.
  tipoPessoaExterna: boolean;
}

// Bloco "Outras pessoas envolvidas (não funcionárias)", logo abaixo de "Funcionários envolvidos"
// (opção A do mockup aprovado: um cartão em linha por pessoa, sem sair da tela).
export function BlocoPessoasExternas({ pessoas, aoMudarLista, tipoPessoaExterna }: Props) {
  const noLimite = pessoas.length >= MAXIMO_PESSOAS_EXTERNAS;
  const temObito = pessoas.some((p) => p.resultado === ResultadoPessoaExterna.Obito);

  return (
    <Field label="Outras pessoas envolvidas (não funcionárias)">
      {() => (
        <div style={estilos.lista}>
          {pessoas.map((p, i) => (
            <CartaoPessoaExterna
              key={p.chave}
              indice={i}
              pessoa={p}
              aoMudar={(patch) => aoMudarLista((atuais) => atuais.map((x) => (x.chave === p.chave ? { ...x, ...patch } : x)))}
              aoRemover={() => aoMudarLista((atuais) => atuais.filter((x) => x.chave !== p.chave))}
            />
          ))}
          {temObito && <LembreteProvidenciasObito />}
          <Button
            appearance="outline"
            style={estilos.adicionar}
            icon={<Add20Regular />}
            disabled={noLimite}
            onClick={() => aoMudarLista((atuais) => [...atuais, novaPessoaExterna()])}
          >
            Adicionar pessoa externa
          </Button>
          {noLimite && <Legenda>Limite de {MAXIMO_PESSOAS_EXTERNAS} pessoas externas por ocorrência.</Legenda>}
          {tipoPessoaExterna && (
            <FeedbackInline tom="info">
              Pessoas externas não geram CAT nem entram nos indicadores de frequência/gravidade da NBR 14280 da empresa;
              ficam registradas na ocorrência e na investigação.
            </FeedbackInline>
          )}
        </div>
      )}
    </Field>
  );
}
