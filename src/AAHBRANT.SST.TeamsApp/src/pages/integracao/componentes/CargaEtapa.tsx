import { useState } from 'react';
import { BarraProgresso, Button, CampoData, Field, Input, StatusChip } from '@ui';
import { Delete20Regular, Flag16Regular } from '@fluentui/react-icons';
import { api, type EtapaIntegracao, type IntegracaoAtendimentoDetalhe } from '../../../lib/api';
import { useEstilosIntegracao } from './estilos';
import { dataBrasil, formatarCarga } from './helpers';

interface Props {
  integracaoId: string;
  etapa: EtapaIntegracao;
  somenteLeitura: boolean;
  aoAtualizar: (detalhe: IntegracaoAtendimentoDetalhe) => void;
  aoErro: (mensagem: string | null) => void;
}

function hojeIso(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function minutosDe(hhmm: string): number | null {
  const m = /^(\d{2}):(\d{2})$/.exec(hhmm);
  return m ? Number(m[1]) * 60 + Number(m[2]) : null;
}

// Carga horária da etapa (mockup v4): "cumprida x mínimo da NR", barra de progresso e a mensagem do que
// falta. A carga é a soma dos períodos lançados (data, início e fim), pois a etapa pode durar mais de
// um dia. Mínimo 0 = sem trava (ex.: NR-06 até o QSMS definir): a tela só pede a carga registrada.
export function CargaEtapa({ integracaoId, etapa, somenteLeitura, aoAtualizar, aoErro }: Props) {
  const e = useEstilosIntegracao();
  const [data, setData] = useState(hojeIso());
  const [inicio, setInicio] = useState('');
  const [fim, setFim] = useState('');
  const [salvando, setSalvando] = useState(false);

  const minimoMin = Math.round(etapa.cargaMinimaHoras * 60);
  const temMinimo = minimoMin > 0;
  const temPeriodo = etapa.periodos.length > 0;
  const cargaOk = temPeriodo && (temMinimo ? etapa.cargaLancadaMinutos >= minimoMin : true);
  const pct = temMinimo ? Math.min(100, Math.round((etapa.cargaLancadaMinutos / minimoMin) * 100)) : etapa.cargaLancadaMinutos > 0 ? 100 : 0;
  const mensagem = temMinimo
    ? cargaOk
      ? `Carga atende o mínimo de ${etapa.cargaMinimaHoras} h.`
      : `Faltam ${formatarCarga(minimoMin - etapa.cargaLancadaMinutos)} para o mínimo de ${etapa.cargaMinimaHoras} h. A etapa só avança com a carga atendida.`
    : cargaOk
      ? 'Sem trava de mínimo até o QSMS definir. Registre a carga cumprida, que vai para o certificado.'
      : 'Registre a carga cumprida.';

  async function adicionar() {
    const ini = minutosDe(inicio);
    const fi = minutosDe(fim);
    if (!data || ini == null || fi == null) {
      aoErro('Informe a data, o horário de início e o de fim do período.');
      return;
    }
    if (fi <= ini) {
      aoErro('O horário de fim precisa ser depois do início.');
      return;
    }
    try {
      setSalvando(true);
      aoErro(null);
      aoAtualizar(await api.integracoesAtendimento.registrarPeriodo(integracaoId, etapa.id, { data, inicio, fim }));
      setInicio('');
      setFim('');
    } catch (err) {
      aoErro(err instanceof Error ? err.message : 'Não foi possível registrar o período.');
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
    <div className={e.carga}>
      <div className={e.cargaLinha}>
        <span className={e.mudo}>Carga horária cumprida</span>
        {temMinimo ? (
          <StatusChip tom="info">Mínimo da NR: {etapa.cargaMinimaHoras} h</StatusChip>
        ) : (
          <span className={e.chipAberto}>
            <Flag16Regular /> Mínimo: a definir pelo QSMS
          </span>
        )}
      </div>
      <div>
        <strong className={e.cargaValor}>{formatarCarga(etapa.cargaLancadaMinutos)}</strong>
        {temMinimo && <span className={e.mudo}> de {etapa.cargaMinimaHoras} h</span>}
      </div>
      <div style={{ margin: '8px 0 6px' }}>
        <BarraProgresso percentual={pct} tom={cargaOk ? 'ok' : 'atencao'} aria-label="Carga horária cumprida" />
      </div>
      <div className={e.mudo}>{mensagem}</div>

      {etapa.periodos.length > 0 && (
        <ul className={e.periodos} style={{ listStyleType: 'none', padding: 0, margin: '10px 0 0' }} aria-label="Períodos lançados">
          {etapa.periodos.map((p) => (
            <li key={p.id} className={e.periodo}>
              <span>
                <b>{dataBrasil(p.data)}</b> · {p.inicio} às {p.fim} · {formatarCarga(p.minutos)}
              </span>
              {!somenteLeitura && (
                <Button
                  appearance="subtle"
                  size="small"
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

      {!somenteLeitura && (
        <div className={e.periodoForm}>
          <Field label="Data">
            <CampoData naoFutura value={data} onChange={(_, d) => setData(d.value)} />
          </Field>
          <Field label="Início">
            <Input type="time" value={inicio} onChange={(_, d) => setInicio(d.value)} />
          </Field>
          <Field label="Fim">
            <Input type="time" value={fim} onChange={(_, d) => setFim(d.value)} />
          </Field>
          <Button appearance="secondary" className={e.btnMedio} disabled={salvando || !inicio || !fim} onClick={() => void adicionar()}>
            Lançar período
          </Button>
        </div>
      )}
    </div>
  );
}
