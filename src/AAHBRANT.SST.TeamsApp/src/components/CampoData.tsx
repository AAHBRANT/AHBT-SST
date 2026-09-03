import { useEffect, useState } from 'react';
import { Input, type InputProps } from '@fluentui/react-components';

interface CampoDataProps extends Omit<InputProps, 'value' | 'onChange' | 'type'> {
  /** Data em ISO (yyyy-MM-dd), ou string vazia se não preenchida — mesmo formato de <Input type="date">. */
  value: string;
  onChange: NonNullable<InputProps['onChange']>;
}

function isoParaExibicao(iso: string): string {
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso ?? '');
  if (!m) return '';
  return `${m[3]}/${m[2]}/${m[1]}`;
}

function digitosParaMascara(digitos: string): string {
  const dia = digitos.slice(0, 2);
  const mes = digitos.slice(2, 4);
  const ano = digitos.slice(4, 8);
  let resultado = dia;
  if (mes) resultado += `/${mes}`;
  if (ano) resultado += `/${ano}`;
  return resultado;
}

function mascaraParaIso(mascara: string): string {
  const digitos = mascara.replace(/\D/g, '');
  if (digitos.length !== 8) return '';
  const dia = Number(digitos.slice(0, 2));
  const mes = Number(digitos.slice(2, 4));
  const ano = Number(digitos.slice(4, 8));
  const data = new Date(ano, mes - 1, dia);
  const valida = data.getFullYear() === ano && data.getMonth() === mes - 1 && data.getDate() === dia;
  if (!valida) return '';
  return `${ano}-${String(mes).padStart(2, '0')}-${String(dia).padStart(2, '0')}`;
}

// Campo de data 100% digitável (pedido do usuário, 02/09): o <input type="date"> nativo obriga
// clicar em cada segmento ou abrir o seletor de calendário do navegador pra trocar dia/mês/ano —
// lento pra digitar uma data de cabeça, principalmente em datas distantes (ex.: data de nascimento
// ou de admissão antiga). Substitui por texto com máscara dd/mm/aaaa: dá pra digitar os 8 dígitos
// direto, sem parar em nenhum segmento nem abrir seletor nenhum. Mantém o mesmo contrato de
// value/onChange de <Input type="date"> (ambos em ISO yyyy-MM-dd) pra ser um substituto direto em
// qualquer formulário existente.
export function CampoData({ value, onChange, ...resto }: CampoDataProps) {
  const [texto, setTexto] = useState(() => isoParaExibicao(value));

  useEffect(() => {
    setTexto(isoParaExibicao(value));
  }, [value]);

  return (
    <Input
      {...resto}
      value={texto}
      placeholder="dd/mm/aaaa"
      inputMode="numeric"
      onChange={(ev, data) => {
        const digitos = data.value.replace(/\D/g, '').slice(0, 8);
        const mascarado = digitosParaMascara(digitos);
        setTexto(mascarado);
        onChange(ev, { value: mascaraParaIso(mascarado) });
      }}
    />
  );
}
