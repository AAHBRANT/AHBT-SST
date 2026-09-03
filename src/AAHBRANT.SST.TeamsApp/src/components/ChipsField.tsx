import { useState, type KeyboardEvent } from 'react';
import { Dismiss12Regular } from '@fluentui/react-icons';
import { usePageStyles } from '../pages/pageStyles';

interface ChipsFieldProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  id?: string;
}

const DELIMITADOR = '; ';

// Campo de "chips" removíveis para um campo de texto que guarda uma lista curta como string única
// delimitada (pedido do usuário, 03/09, réplica de mockup) — ver PcmsoTab.tsx/PcmsoDetalhePage.tsx
// (Unidades/Obras abrangidas). Não é um seletor com opções pré-definidas (diferente de
// useCheckboxChipStyles em pageStyles.ts): cada chip é um trecho de texto livre que o usuário digitou
// e confirmou com Enter, vírgula ou ao sair do campo.
export function ChipsField({ value, onChange, placeholder, id }: ChipsFieldProps) {
  const estilos = usePageStyles();
  const [rascunho, setRascunho] = useState('');

  const itens = value ? value.split(DELIMITADOR).filter(Boolean) : [];

  function adicionar() {
    const texto = rascunho.trim();
    if (!texto) return;
    onChange([...itens, texto].join(DELIMITADOR));
    setRascunho('');
  }

  function remover(indice: number) {
    onChange(itens.filter((_, i) => i !== indice).join(DELIMITADOR));
  }

  function aoTeclar(ev: KeyboardEvent<HTMLInputElement>) {
    if (ev.key === 'Enter' || ev.key === ',') {
      ev.preventDefault();
      adicionar();
    } else if (ev.key === 'Backspace' && !rascunho && itens.length > 0) {
      remover(itens.length - 1);
    }
  }

  return (
    <div className={estilos.chipsContainer}>
      {itens.map((item, indice) => (
        <span key={`${item}-${indice}`} className={estilos.chip}>
          {item}
          <Dismiss12Regular className={estilos.chipRemove} onClick={() => remover(indice)} />
        </span>
      ))}
      <input
        id={id}
        className={estilos.chipsInput}
        value={rascunho}
        placeholder={itens.length === 0 ? placeholder : undefined}
        onChange={(ev) => setRascunho(ev.target.value)}
        onKeyDown={aoTeclar}
        onBlur={adicionar}
      />
    </div>
  );
}
