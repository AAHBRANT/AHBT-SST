import { Eye20Regular, PauseCircle20Regular, Send20Regular } from '@fluentui/react-icons';
import { Campo, Field, FormGrid, FormSection, Input, Switch, useTipografia } from '@ui';
import {
  ajudaNaoRepetir,
  ajudaPersistencia,
  estadoDoAviso,
  fraseResultado,
  LIMITE_NAO_REPETIR,
  LIMITE_PERSISTENCIA,
  lerSegundos,
  mensagemLimite,
  PERSISTENCIA_PADRAO,
  type ItemRegra,
  type LimiteRegra,
} from './regrasAlerta';
import { mergeClasses, useEstilosRegrasAlerta } from './estilosRegrasAlerta';

// Textos digitados (string) para o campo poder ficar vazio enquanto a pessoa digita; a validação é no salvar.
export interface RegrasAlertaTextos {
  capacete: string;
  colete: string;
  oculos: string;
  naoRepetir: string;
}

export interface RegrasAlertaCameraProps {
  valores: RegrasAlertaTextos;
  aoMudar: (parcial: Partial<RegrasAlertaTextos>) => void;
  modoSombra: boolean;
  aoMudarModoSombra: (ligado: boolean) => void;
  avisarTelegram: boolean;
  // Cadastro de câmera nova: mostra o aviso de que ela começa em modo sombra.
  cameraNova: boolean;
}

// Etapa 3 do cadastro da câmera: confirmação por segundos, "não repetir" e modo sombra.
// "Intervalo entre alertas" e "Avisar no Telegram" continuam na Etapa 1.
export function RegrasAlertaCamera({
  valores,
  aoMudar,
  modoSombra,
  aoMudarModoSombra,
  avisarTelegram,
  cameraNova,
}: RegrasAlertaCameraProps) {
  const e = useEstilosRegrasAlerta();
  const tipo = useTipografia();

  const cap = lerSegundos(valores.capacete, LIMITE_PERSISTENCIA);
  const col = lerSegundos(valores.colete, LIMITE_PERSISTENCIA);
  const ocu = lerSegundos(valores.oculos, LIMITE_PERSISTENCIA);
  const rep = lerSegundos(valores.naoRepetir, LIMITE_NAO_REPETIR);

  const estado = estadoDoAviso(modoSombra, avisarTelegram);
  // Número inválido na frase de resultado usa o padrão (o erro aparece embaixo do campo).
  const frase = fraseResultado(estado, {
    capacete: cap ?? PERSISTENCIA_PADRAO,
    colete: col ?? PERSISTENCIA_PADRAO,
    oculos: ocu ?? PERSISTENCIA_PADRAO,
  });

  function campo(
    rotulo: string,
    campoChave: keyof RegrasAlertaTextos,
    valido: number | null,
    limite: LimiteRegra,
    ajuda: (n: number) => string,
  ) {
    return (
      <>
        <Field label={rotulo}>
          <Input
            className={e.campoNumero}
            type="number"
            inputMode="numeric"
            min={limite.min}
            max={limite.max}
            value={valores[campoChave]}
            aria-invalid={valido === null}
            contentAfter={<span>segundos</span>}
            onChange={(_, d) => aoMudar({ [campoChave]: d.value })}
          />
        </Field>
        <div className={mergeClasses(tipo.legenda, e.dica, valido === null && e.dicaErro)}>
          {valido === null ? mensagemLimite(limite) : ajuda(valido)}
        </div>
      </>
    );
  }

  const persist = (item: ItemRegra) => (n: number) => ajudaPersistencia(item, n);
  const Icone = estado === 'avisa' ? Send20Regular : estado === 'sombra' ? Eye20Regular : PauseCircle20Regular;

  return (
    <FormSection titulo="Regras de alerta" numero={3}>
      <FormGrid>
        <Campo span={6}>{campo('Confirmar falta de capacete', 'capacete', cap, LIMITE_PERSISTENCIA, persist('capacete'))}</Campo>
        <Campo span={6}>{campo('Confirmar falta de colete', 'colete', col, LIMITE_PERSISTENCIA, persist('colete'))}</Campo>
        <Campo span={6}>{campo('Confirmar falta de óculos', 'oculos', ocu, LIMITE_PERSISTENCIA, persist('oculos'))}</Campo>
        <Campo span={6}>
          {campo('Não repetir alerta da mesma pessoa por', 'naoRepetir', rep, LIMITE_NAO_REPETIR, ajudaNaoRepetir)}
        </Campo>
      </FormGrid>

      <div className={e.caixaSombra}>
        <Switch checked={modoSombra} label="Modo sombra" onChange={(_, d) => aoMudarModoSombra(d.checked)} />
        <div className={mergeClasses(tipo.legenda, e.dica, e.caixaSombraTexto)}>
          A câmera analisa e registra as ocorrências no sistema, mas não envia nada no Telegram. Use para conferir os acertos
          antes de ligar os avisos.
        </div>
        {cameraNova && (
          <div className={mergeClasses(tipo.legenda, e.dica, e.destaqueInfo)}>Câmera nova começa em modo sombra.</div>
        )}
      </div>

      <div className={e.resultado} aria-live="polite">
        <span
          aria-hidden="true"
          className={mergeClasses(
            e.resultadoIcone,
            estado === 'avisa' && e.resultadoAvisa,
            estado === 'sombra' && e.resultadoSombra,
            estado === 'mudo' && e.resultadoMudo,
          )}
        >
          <Icone />
        </span>
        <span className={tipo.corpo}>{frase}</span>
      </div>
    </FormSection>
  );
}
