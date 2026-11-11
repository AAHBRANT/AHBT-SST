import { Checkmark16Regular, LockClosed16Regular } from '@fluentui/react-icons';
import type { EtapaIntegracao } from '../../../lib/api';
import { mergeClasses, useEstilosIntegracao } from './estilos';
import { TIPOS_EM_ORDEM, TOTAL_ETAPAS, rotuloEtapa } from './helpers';

interface Props {
  etapas: EtapaIntegracao[];
  /** Ordem da etapa em andamento; ignorada com a integração concluída. */
  etapaAtualOrdem?: number | null;
  concluida?: boolean;
  /** Antes de iniciar: todas aparecem bloqueadas, na ordem fixa. */
  previa?: boolean;
}

const ETAPAS_PREVIA: Pick<EtapaIntegracao, 'ordem' | 'tipo'>[] = TIPOS_EM_ORDEM.map((tipo, i) => ({ ordem: i + 1, tipo }));

// Trilha das 5 etapas, sempre visível no topo (mockup v4). Estados: concluída (verde, com o nº de
// assinaturas), em andamento (vinho), bloqueada (cinza, cadeado) e pulada ("Ninguém precisa"). No
// celular fica compacta: círculos, e só a etapa atual mostra o nome.
export function StepperEtapas({ etapas, etapaAtualOrdem, concluida = false, previa = false }: Props) {
  const e = useEstilosIntegracao();
  const lista: Array<Pick<EtapaIntegracao, 'ordem' | 'tipo'> & Partial<EtapaIntegracao>> = previa
    ? ETAPAS_PREVIA
    : [...etapas].sort((a, b) => a.ordem - b.ordem);

  const legenda = (() => {
    if (concluida) return <><b>{TOTAL_ETAPAS} de {TOTAL_ETAPAS} etapas concluídas.</b></>;
    if (previa || etapaAtualOrdem == null) return 'A trilha mostra a ordem fixa: NR-06, NR-12, NR-18, NR-35 e Ordem de Serviço.';
    const atual = lista.find((x) => x.ordem === etapaAtualOrdem);
    return (
      <>
        <b>Etapa {etapaAtualOrdem} de {TOTAL_ETAPAS}{atual ? ` · ${rotuloEtapa(atual.tipo).curto}` : ''}.</b> Cada etapa só libera a próxima depois de concluída.
      </>
    );
  })();

  return (
    <div>
      <ol className={e.trilha} aria-label="Etapas da integração">
        {lista.map((et) => {
          const rotulo = rotuloEtapa(et.tipo);
          const pulada = !previa && et.status === 'pulada';
          const feita = !previa && !pulada && (concluida || et.status === 'concluida');
          const atual = !previa && !concluida && !feita && !pulada && et.ordem === etapaAtualOrdem;
          const atualPulada = !previa && !concluida && pulada && et.ordem === etapaAtualOrdem;
          let sub: string;
          if (pulada) sub = 'Ninguém precisa';
          else if (feita) sub = et.tipo === 'ordemServico' ? 'Concluída' : `${(et.participantes ?? []).filter((p) => !p.naoAssinou).length} assinaturas`;
          else if (atual) sub = 'Em andamento';
          else sub = 'Bloqueada';

          const mostrarTexto = atual || atualPulada;
          return (
            <li
              key={et.ordem}
              aria-label={`Etapa ${et.ordem}: ${rotulo.curto}, ${sub}`}
              aria-current={atual || atualPulada ? 'step' : undefined}
              className={mergeClasses(
                e.etapa,
                feita && e.etapaFeita,
                atual && e.etapaAtual,
                atualPulada && e.etapaAtual,
                !feita && !atual && !pulada && e.etapaTravada,
                pulada && !atualPulada && e.etapaPulada,
              )}
            >
              <span
                className={mergeClasses(
                  e.numero,
                  feita && e.numeroFeito,
                  (atual || atualPulada) && e.numeroNaTrilhaAtual,
                )}
                aria-hidden="true"
              >
                {pulada ? '–' : feita ? <Checkmark16Regular /> : atual ? et.ordem : <LockClosed16Regular />}
              </span>
              <span className={mergeClasses(e.etapaTexto, mostrarTexto && e.etapaAtualTexto)}>
                <span className={mergeClasses(e.etapaNome, feita && e.etapaFeitaTexto)}>{rotulo.curto}</span>
                <span className={mergeClasses(e.etapaSub, feita && e.etapaFeitaTexto, mostrarTexto && e.etapaAtualSub)}>{sub}</span>
              </span>
            </li>
          );
        })}
      </ol>
      <div className={e.legendaTrilha}>{legenda}</div>
    </div>
  );
}
