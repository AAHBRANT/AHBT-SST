import { Card, StatusChip, useEstilosEsmeralda } from '@ui';
import { Clock16Regular } from '@fluentui/react-icons';
import { type EtapaIntegracao, type IntegracaoAtendimentoDetalhe } from '../../../lib/api';
import { mergeClasses, useEstilosIntegracao } from './estilos';
import { dataBrasil, formatarCarga } from './helpers';
import {
  cargaDecorridaMin,
  estadoCarga,
  fimCargaAutomaticaMs,
  formatarHoraBrasil,
  percentualCarga,
  textoFechaAs,
} from './helpersInstrutor';
import { TituloSecao } from './pecas';

interface Props {
  integracaoId: string;
  etapa: EtapaIntegracao;
  codigoCurso: string;
  somenteLeitura: boolean;
  /** Relógio da tela (ms): a barra e a espera do fim recalculam por ele. */
  agoraMs: number;
  aoAtualizar: (detalhe: IntegracaoAtendimentoDetalhe) => void;
  aoErro: (mensagem: string | null) => void;
}

// Carga horária da etapa (16/11/2026; sem nenhum caminho manual desde 17/11/2026): AUTOMÁTICA e só leitura. O servidor
// lança o período ao abrir a etapa (início = hora da abertura, fim = início + carga prevista); ninguém ajusta nem lança
// horário. A tela mostra o período, a barra (esmeralda, igual ao KpiCard) e, enquanto o fim não chega, "Fecha às HH:mm".
// O Avançar só libera depois do fim (regra do servidor). Props de ação ficam na interface por compatibilidade com quem chama.
export function CargaEtapa({ etapa, codigoCurso, agoraMs }: Props) {
  const e = useEstilosIntegracao();
  const esm = useEstilosEsmeralda();

  const periodos = etapa.periodos ?? [];
  const minimoMin = Math.round(etapa.cargaMinimaHoras * 60);
  const temMinimo = minimoMin > 0;
  const estado = estadoCarga(etapa, agoraMs);
  const fimAuto = fimCargaAutomaticaMs(periodos);
  const temAuto = periodos.some((p) => p.automatico);
  const pct = percentualCarga(etapa, agoraMs);
  const decorrida = cargaDecorridaMin(periodos, agoraMs);

  const chip =
    estado === 'cumprida' ? (
      <StatusChip tom="ok">Carga cumprida</StatusChip>
    ) : estado === 'aguardando_fim' && fimAuto != null ? (
      <StatusChip tom="neutro" icone={<Clock16Regular />}>Fecha às {textoFechaAs(fimAuto, agoraMs)}</StatusChip>
    ) : estado === 'abaixo_minimo' ? (
      <StatusChip tom="alerta">Abaixo do mínimo</StatusChip>
    ) : (
      <StatusChip tom="neutro" icone={<Clock16Regular />}>{temMinimo ? 'Contando' : 'Registrada ao avançar'}</StatusChip>
    );

  const mensagem = (() => {
    if (estado === 'aguardando_fim' && fimAuto != null) {
      return `Passaram ${formatarCarga(decorrida)}${temMinimo ? ` de ${etapa.cargaMinimaHoras} h` : ''}. O período fecha sozinho às ${textoFechaAs(fimAuto, agoraMs)}, sem você confirmar nada.`;
    }
    if (estado === 'cumprida') {
      return temAuto && fimAuto != null
        ? `O período fechou às ${formatarHoraBrasil(fimAuto)}${temMinimo ? ` e atende o mínimo de ${etapa.cargaMinimaHoras} h da ${codigoCurso}` : ''}.`
        : temMinimo
          ? `Carga atende o mínimo de ${etapa.cargaMinimaHoras} h.`
          : 'A carga registrada vai para o certificado.';
    }
    if (estado === 'abaixo_minimo') {
      return `A carga ainda está abaixo do mínimo de ${etapa.cargaMinimaHoras} h da ${codigoCurso}. Ela é contada automaticamente desde a abertura da etapa.`;
    }
    return temMinimo
      ? 'A carga será contada automaticamente desde a abertura da etapa.'
      : 'Sem carga prevista: o período é registrado sozinho ao avançar, da abertura da etapa até a hora do avanço.';
  })();

  return (
    <Card
      titulo={
        <TituloSecao numero={<Clock16Regular />} estado={estado === 'cumprida' ? 'feito' : 'atual'}>
          Carga horária
        </TituloSecao>
      }
      acoes={chip}
      subtitulo={`Contada automaticamente desde a abertura da etapa${temMinimo ? ` (carga prevista: ${etapa.cargaMinimaHoras} h)` : ''}.`}
    >
      <div>
        {periodos.length > 0 && (
          <ul style={{ listStyleType: 'none', padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: 8 }} aria-label="Períodos lançados">
            {periodos.map((p) => (
              <li key={p.id} className={e.periodoAuto}>
                <div>
                  <div className={e.mudo}>{p.automatico ? 'Período lançado automaticamente' : 'Período lançado manualmente'}</div>
                  <b>
                    {dataBrasil(p.data)} · {p.inicio} às {p.fim} · {formatarCarga(p.minutos)}
                  </b>
                </div>
              </li>
            ))}
          </ul>
        )}
        {periodos.length > 0 && (
          <div className={e.barraTrilho} role="progressbar" aria-valuenow={pct} aria-valuemin={0} aria-valuemax={100} aria-label="Carga horária cumprida">
            <div className={mergeClasses(e.barraPreench, esm.vidro)} style={{ width: `${pct}%`, display: pct > 0 ? undefined : 'none' }} />
          </div>
        )}
        <div className={e.mudo} style={{ marginTop: 8 }}>
          {mensagem}
        </div>
      </div>
    </Card>
  );
}
