import { useState } from 'react';
import { Button, Card, CampoData, Field, Input, StatusChip, useEstilosEsmeralda } from '@ui';
import { Clock16Regular, Delete20Regular, Edit16Regular, Warning16Regular } from '@fluentui/react-icons';
import { api, type EtapaIntegracao, type IntegracaoAtendimentoDetalhe } from '../../../lib/api';
import { mergeClasses, useEstilosIntegracao } from './estilos';
import { dataBrasil, formatarCarga } from './helpers';
import {
  aberturaEtapaMs,
  avisoAbaixoDoMinimo,
  cargaDecorridaMin,
  corpoAjustePeriodos,
  estadoCarga,
  fimCargaAutomaticaMs,
  formatarHoraBrasil,
  hojeIsoBrasilia,
  instanteBrasilia,
  periodosSobrepostos,
  percentualCarga,
  textoFechaAs,
  validarPeriodosAjuste,
  type PeriodoAjuste,
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

// Carga horária da etapa (16/11/2026, mockup v3 e decisão 1 do usuário): AUTOMÁTICA. O servidor lança o
// período ao abrir a etapa (início = hora da abertura, fim = início + carga prevista); o técnico não confirma
// nada. A tela mostra o período, a barra (esmeralda, igual ao KpiCard) e, enquanto o fim não chega, "Fecha às
// HH:mm". "Ajustar horário" abre o formulário manual (data, início e fim), que é o único caminho manual:
// substitui o período automático pelo informado numa só chamada (se falhar, nada muda). O Avançar só libera depois do fim (regra do servidor).
export function CargaEtapa({ integracaoId, etapa, codigoCurso, somenteLeitura, agoraMs, aoAtualizar, aoErro }: Props) {
  const e = useEstilosIntegracao();
  const esm = useEstilosEsmeralda();
  const [ajustando, setAjustando] = useState(false);
  // Linhas do ajuste: uma por período automático (a janela que cruza a meia-noite vira dois); ao lançar do zero, uma só.
  const [linhas, setLinhas] = useState<PeriodoAjuste[]>([]);
  const [salvando, setSalvando] = useState(false);
  const [erroForm, setErroForm] = useState<string | null>(null);

  const periodos = etapa.periodos ?? [];
  const minimoMin = Math.round(etapa.cargaMinimaHoras * 60);
  const temMinimo = minimoMin > 0;
  const estado = estadoCarga(etapa, agoraMs);
  const fimAuto = fimCargaAutomaticaMs(periodos);
  const temAuto = periodos.some((p) => p.automatico);
  const pct = percentualCarga(etapa, agoraMs);
  const decorrida = cargaDecorridaMin(periodos, agoraMs);
  const abertura = aberturaEtapaMs(etapa.iniciadaEmUtc);
  // Aviso de mínimo: soma o que o ajuste deixa (linhas válidas) com os manuais que ficam.
  const avisoMinimo = ajustando && linhas.length === 1 ? avisoAbaixoDoMinimo(linhas[0].inicio, linhas[0].fim, etapa.cargaMinimaHoras) : null;

  const chip =
    estado === 'cumprida' ? (
      <StatusChip tom="ok">Carga cumprida</StatusChip>
    ) : estado === 'aguardando_fim' && fimAuto != null ? (
      <StatusChip tom="neutro" icone={<Clock16Regular />}>Fecha às {textoFechaAs(fimAuto, agoraMs)}</StatusChip>
    ) : estado === 'abaixo_minimo' ? (
      <StatusChip tom="alerta">Abaixo do mínimo</StatusChip>
    ) : (
      <StatusChip tom="atencao">Falta lançar</StatusChip>
    );

  const mensagem = (() => {
    if (estado === 'aguardando_fim' && fimAuto != null) {
      return `Passaram ${formatarCarga(decorrida)}${temMinimo ? ` de ${etapa.cargaMinimaHoras} h` : ''}. O período fecha sozinho às ${textoFechaAs(fimAuto, agoraMs)}, sem você confirmar nada. Para encerrar antes, use "Ajustar horário".`;
    }
    if (estado === 'cumprida') {
      return temAuto && fimAuto != null
        ? `O período fechou às ${formatarHoraBrasil(fimAuto)}${temMinimo ? ` e atende o mínimo de ${etapa.cargaMinimaHoras} h da ${codigoCurso}` : ''}.`
        : temMinimo
          ? `Carga atende o mínimo de ${etapa.cargaMinimaHoras} h.`
          : 'Sem trava de mínimo até o QSMS definir. A carga registrada vai para o certificado.';
    }
    if (estado === 'abaixo_minimo') {
      return `Faltam ${formatarCarga(minimoMin - etapa.cargaLancadaMinutos)} para o mínimo de ${etapa.cargaMinimaHoras} h da ${codigoCurso}. A etapa só avança com a carga atendida.`;
    }
    return temMinimo
      ? 'Nenhum período lançado. Lance o período com "Lançar período"; a etapa só avança com a carga atendida.'
      : 'Nenhum período lançado. Registre a carga cumprida, que vai para o certificado.';
  })();

  function abrirAjuste() {
    const hoje = hojeIsoBrasilia(agoraMs);
    const agoraHhmm = formatarHoraBrasil(agoraMs);
    const autos = periodos
      .filter((p) => p.automatico)
      .map((p) => ({ data: p.data.slice(0, 10), inicio: p.inicio, fim: p.fim }))
      .sort((x, y) => instanteBrasilia(x.data, x.inicio) - instanteBrasilia(y.data, y.inicio));
    // Linha que ainda não começou (a 2ª de uma janela que cruza a meia-noite, antes da meia-noite) não entra: não dá para ajustar o futuro.
    const jaComecou = autos.filter((p) => instanteBrasilia(p.data, p.inicio) <= agoraMs);
    const base = jaComecou.length > 0 ? jaComecou : autos.slice(0, 1);
    const iniciais: PeriodoAjuste[] = base.length > 0 ? base : [{ data: hoje, inicio: '', fim: '' }];
    // Sugere encerrar agora na última linha (o fim não pode estar no futuro), sem passar do fim previsto.
    const ultima = iniciais[iniciais.length - 1];
    if (ultima.data === hoje && agoraHhmm && (!ultima.fim || agoraHhmm < ultima.fim)) ultima.fim = agoraHhmm;
    setLinhas(iniciais);
    setErroForm(null);
    setAjustando(true);
  }

  function editarLinha(i: number, parte: Partial<PeriodoAjuste>) {
    setLinhas((atual) => atual.map((l, idx) => (idx === i ? { ...l, ...parte } : l)));
  }

  async function salvar() {
    const agora = Date.now();
    const erro = validarPeriodosAjuste(linhas, agora, abertura);
    if (erro) {
      setErroForm(erro);
      return;
    }
    try {
      setSalvando(true);
      setErroForm(null);
      aoErro(null);
      if (temAuto) {
        // UMA chamada atômica: o servidor substitui todos os períodos da etapa (os manuais seguem na lista).
        const corpo = corpoAjustePeriodos(linhas, periodos);
        if (periodosSobrepostos(corpo)) {
          setErroForm('Os períodos não podem se sobrepor aos que a etapa já tem.');
          return;
        }
        aoAtualizar(await api.integracoesAtendimento.ajustarPeriodos(integracaoId, etapa.id, corpo));
      } else {
        // Sem período automático (lançar do zero ou outro período): é só acrescentar.
        const { data, inicio, fim } = linhas[0];
        aoAtualizar(await api.integracoesAtendimento.registrarPeriodo(integracaoId, etapa.id, { data, inicio, fim }));
      }
      setAjustando(false);
    } catch (err) {
      // Mensagem do servidor na tela, com o formulário como estava (nada foi perdido: a chamada é atômica).
      setErroForm(err instanceof Error ? err.message : 'Não foi possível salvar o ajuste.');
    } finally {
      setSalvando(false);
    }
  }

  async function remover(periodoId: string) {
    try {
      setSalvando(true);
      aoErro(null);
      aoAtualizar(await api.integracoesAtendimento.removerPeriodo(integracaoId, etapa.id, periodoId));
    } catch (err) {
      aoErro(err instanceof Error ? err.message : 'Não foi possível remover o período.');
    } finally {
      setSalvando(false);
    }
  }

  return (
    <Card
      titulo={
        <TituloSecao numero={<Clock16Regular />} estado={estado === 'cumprida' ? 'feito' : 'atual'}>
          Carga horária
        </TituloSecao>
      }
      acoes={chip}
      subtitulo={`Lançada ao abrir a etapa: início = hora da abertura, fim = início + carga prevista do curso${temMinimo ? ` (${etapa.cargaMinimaHoras} h)` : ''}.`}
    >
      {ajustando && !somenteLeitura ? (
        <div>
          {linhas.map((l, i) => (
            <div key={i} role="group" aria-label={linhas.length > 1 ? `Período ${i + 1}` : 'Período'}>
              {linhas.length > 1 && <div className={e.mudo}>Período {i + 1}</div>}
              <div className={e.ajusteGrade}>
                <Field label="Data">
                  <CampoData naoFutura value={l.data} onChange={(_, d) => editarLinha(i, { data: d.value })} />
                </Field>
                <Field label="Início">
                  <Input type="time" value={l.inicio} onChange={(_, d) => editarLinha(i, { inicio: d.value })} />
                </Field>
                <Field label="Fim">
                  <Input type="time" value={l.fim} onChange={(_, d) => editarLinha(i, { fim: d.value })} />
                </Field>
              </div>
              {linhas.length > 1 && (
                <Button
                  appearance="subtle"
                  className={e.btnMedio}
                  icon={<Delete20Regular />}
                  disabled={salvando}
                  onClick={() => setLinhas((atual) => atual.filter((_, idx) => idx !== i))}
                >
                  Tirar o período {i + 1}
                </Button>
              )}
            </div>
          ))}
          {avisoMinimo && (
            <div className={e.avisoAjuste} role="status">
              <Warning16Regular aria-hidden="true" />
              <div>
                <b>{avisoMinimo.split('. ')[0]}.</b> {avisoMinimo.split('. ').slice(1).join('. ')}
              </div>
            </div>
          )}
          {erroForm && (
            <div className={e.motivo} role="alert">
              <Warning16Regular aria-hidden="true" />
              <span>{erroForm}</span>
            </div>
          )}
          <div className={e.acoesLivres}>
            <Button appearance="primary" className={e.btnGrande} disabled={salvando || linhas.length === 0 || linhas.some((l) => !l.inicio || !l.fim)} onClick={() => void salvar()}>
              Salvar ajuste
            </Button>
            <Button className={e.btnGrande} disabled={salvando} onClick={() => setAjustando(false)}>
              Cancelar
            </Button>
          </div>
        </div>
      ) : (
        <div>
          {periodos.length > 0 && (
            <ul style={{ listStyleType: 'none', padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: 8 }} aria-label="Períodos lançados">
              {periodos.map((p, i) => (
                <li key={p.id} className={e.periodoAuto}>
                  <div>
                    <div className={e.mudo}>{p.automatico ? 'Período lançado automaticamente' : 'Período lançado manualmente'}</div>
                    <b>
                      {dataBrasil(p.data)} · {p.inicio} às {p.fim} · {formatarCarga(p.minutos)}
                    </b>
                  </div>
                  {!somenteLeitura && p.automatico && i === periodos.findIndex((x) => x.automatico) && (
                    <Button appearance="subtle" className={e.btnMedio} icon={<Edit16Regular />} onClick={abrirAjuste}>
                      Ajustar horário
                    </Button>
                  )}
                  {!somenteLeitura && !p.automatico && (
                    <Button
                      appearance="subtle"
                      className={e.btnMedio}
                      icon={<Delete20Regular />}
                      aria-label={`Remover o período de ${dataBrasil(p.data)}, ${p.inicio} às ${p.fim}`}
                      disabled={salvando}
                      onClick={() => void remover(p.id)}
                    />
                  )}
                </li>
              ))}
            </ul>
          )}
          {periodos.length === 0 && !somenteLeitura && (
            <Button appearance="primary" className={e.btnGrande} onClick={abrirAjuste}>
              Lançar período
            </Button>
          )}
          {periodos.length > 0 && (
            <div className={e.barraTrilho} role="progressbar" aria-valuenow={pct} aria-valuemin={0} aria-valuemax={100} aria-label="Carga horária cumprida">
              <div className={mergeClasses(e.barraPreench, esm.vidro)} style={{ width: `${pct}%`, display: pct > 0 ? undefined : 'none' }} />
            </div>
          )}
          <div className={e.mudo} style={{ marginTop: 8 }}>
            {mensagem}
          </div>
          {!somenteLeitura && !temAuto && periodos.length > 0 && (
            <div className={e.acoesLivres}>
              <Button className={e.btnMedio} onClick={abrirAjuste}>
                Lançar outro período
              </Button>
            </div>
          )}
        </div>
      )}
    </Card>
  );
}
