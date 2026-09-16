import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { Radio, RadioGroup, tokensUi, type Tom } from '@ui';
import { StatusItemChecklist, statusItemChecklistLabel } from '../../lib/api';

// Mesmo esquema de cor de tomPorStatusItem em InspecaoDetalhePage.tsx (verde/amarelo da planilha
// "Patrulha de Segurança do Trabalho"): Conforme=ok, NaoConforme=atencao, NaoAplicavel=info.
// Duplicado aqui (em vez de importado) porque o mapeamento de lá não é exportado — mesmo valor,
// dois componentes independentes.
const TOM_POR_STATUS_ITEM: Record<number, Tom> = {
  [StatusItemChecklist.Conforme]: 'ok',
  [StatusItemChecklist.NaoConforme]: 'atencao',
  [StatusItemChecklist.NaoAplicavel]: 'info',
};

const useEstilos = makeStyles({
  rotulo: { fontSize: '11px', fontWeight: 600 },
  ok: { color: tokensUi.status.ok.tinta },
  atencao: { color: tokensUi.status.atencao.tinta },
  info: { color: tokensUi.status.info.tinta },
});

export interface SeletorStatusItemChecklistProps {
  value: number | null;
  onChange: (valor: number) => void;
  disabled?: boolean;
}

// Seletor de 3 bolinhas (Conforme/Não conforme/Não aplicável) para o checklist de Alojamento —
// mesma semântica de tomPorStatusItem, mas como controle de entrada em vez de chip de leitura.
// Usa Radio/RadioGroup do Fluent (já reexportados por @ui com a11y correta: role="radiogroup",
// navegação por teclado e foco visível vêm de fábrica) em vez de <input type="radio"> cru.
export function SeletorStatusItemChecklist({ value, onChange, disabled }: SeletorStatusItemChecklistProps) {
  const estilos = useEstilos();
  const classePorTom: Partial<Record<Tom, string>> = {
    ok: estilos.ok,
    atencao: estilos.atencao,
    info: estilos.info,
  };

  return (
    <RadioGroup
      layout="horizontal"
      disabled={disabled}
      value={value == null ? '' : String(value)}
      onChange={(_, dados) => onChange(Number(dados.value))}
    >
      {Object.values(StatusItemChecklist).map((valor) => (
        <Radio
          key={valor}
          value={String(valor)}
          labelPosition="below"
          label={
            <span className={mergeClasses(estilos.rotulo, classePorTom[TOM_POR_STATUS_ITEM[valor]])}>
              {statusItemChecklistLabel[valor]}
            </span>
          }
        />
      ))}
    </RadioGroup>
  );
}
