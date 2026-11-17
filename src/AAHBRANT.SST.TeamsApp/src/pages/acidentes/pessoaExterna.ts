import type { ItemPendencia } from '../../lib/avisoErro';
import {
  ResultadoPessoaExterna,
  VinculoPessoaExterna,
  type PessoaExternaPayload,
  type PessoaExternaSugerida,
} from '../../lib/api';

// Pessoa que não é funcionária (terceiro, visitante, transeunte) em edição no formulário da ocorrência.
// Campos de texto ficam como string (inputs); a conversão para o corpo da API está em pessoaParaPayload.
export interface PessoaExternaEmEdicao {
  chave: string;
  nome: string;
  // null = ainda não escolhido: o técnico precisa tocar em uma das 4 opções.
  vinculo: number | null;
  empresa: string;
  cpf: string;
  idade: string;
  telefone: string;
  lesao: string;
  atendimento: string;
  resultado: number;
  // Veio do relato por IA e ainda não foi mexida pelo técnico (mostra o selo "IA").
  sugeridaPelaIa?: boolean;
}

let sequencia = 0;
export const novaChavePessoaExterna = () => `pe-${Date.now()}-${++sequencia}`;

export function novaPessoaExterna(parcial: Partial<PessoaExternaEmEdicao> = {}): PessoaExternaEmEdicao {
  return {
    chave: novaChavePessoaExterna(),
    nome: '',
    vinculo: null,
    empresa: '',
    cpf: '',
    idade: '',
    telefone: '',
    lesao: '',
    atendimento: '',
    resultado: ResultadoPessoaExterna.SemLesao,
    ...parcial,
  };
}

export function pessoaDaSugestao(s: PessoaExternaSugerida): PessoaExternaEmEdicao {
  const vinculoValido = [1, 2, 3, 4].includes(s.vinculo) ? s.vinculo : null;
  return novaPessoaExterna({ nome: s.nome.trim(), vinculo: vinculoValido, sugeridaPelaIa: true });
}

export const idCampoPessoaExterna = (chave: string, campo: string) => `oco-pessoa-externa-${chave}-${campo}`;

export const apenasDigitos = (valor: string) => valor.replace(/\D/g, '');

export function formatarTelefone(valor: string): string {
  const d = apenasDigitos(valor).slice(0, 11);
  if (d.length <= 2) return d;
  const ddd = `(${d.slice(0, 2)}) `;
  if (d.length <= 6) return ddd + d.slice(2);
  if (d.length <= 10) return `${ddd}${d.slice(2, 6)}-${d.slice(6)}`;
  return `${ddd}${d.slice(2, 7)}-${d.slice(7)}`;
}

function cpfEhValido(digitos: string): boolean {
  if (digitos.length !== 11 || /^(\d)\1{10}$/.test(digitos)) return false;
  const digito = (base: number) => {
    let soma = 0;
    for (let i = 0; i < base; i++) soma += Number(digitos[i]) * (base + 1 - i);
    const resto = (soma * 10) % 11;
    return resto === 10 ? 0 : resto;
  };
  return digito(9) === Number(digitos[9]) && digito(10) === Number(digitos[10]);
}

export function pendenciasPessoaExterna(p: PessoaExternaEmEdicao, indice: number): ItemPendencia[] {
  const rotulo = `Pessoa externa ${indice + 1}`;
  const itens: ItemPendencia[] = [];
  if (!p.nome.trim())
    itens.push({ nome: `${rotulo}: nome`, dica: 'Informe o nome da pessoa', alvo: idCampoPessoaExterna(p.chave, 'nome') });
  if (p.vinculo === null)
    itens.push({ nome: `${rotulo}: vínculo com a obra`, dica: 'Toque em uma das opções', alvo: idCampoPessoaExterna(p.chave, 'vinculo') });
  const cpf = apenasDigitos(p.cpf);
  if (cpf && !cpfEhValido(cpf))
    itens.push({ nome: `${rotulo}: CPF`, dica: 'Confira os 11 números ou deixe em branco', alvo: idCampoPessoaExterna(p.chave, 'cpf') });
  const telefone = apenasDigitos(p.telefone);
  if (telefone && (telefone.length < 10 || telefone.length > 13))
    itens.push({ nome: `${rotulo}: telefone`, dica: 'Informe o DDD e o número ou deixe em branco', alvo: idCampoPessoaExterna(p.chave, 'telefone') });
  if (p.idade.trim() && (!Number.isInteger(Number(p.idade)) || Number(p.idade) < 0 || Number(p.idade) > 120))
    itens.push({ nome: `${rotulo}: idade`, dica: 'Use um número de 0 a 120 ou deixe em branco', alvo: idCampoPessoaExterna(p.chave, 'idade') });
  return itens;
}

export function pessoaParaPayload(p: PessoaExternaEmEdicao): PessoaExternaPayload {
  const comLesao = p.resultado !== ResultadoPessoaExterna.SemLesao;
  const cpf = apenasDigitos(p.cpf);
  const telefone = apenasDigitos(p.telefone);
  return {
    nome: p.nome.trim(),
    vinculo: p.vinculo ?? VinculoPessoaExterna.Outro,
    empresa: p.vinculo === VinculoPessoaExterna.Terceiro ? p.empresa.trim() || null : null,
    cpf: cpf || null,
    telefone: telefone || null,
    idade: p.idade.trim() ? Number(p.idade) : null,
    lesao: comLesao ? p.lesao.trim() || null : null,
    atendimento: comLesao ? p.atendimento.trim() || null : null,
    resultado: p.resultado,
  };
}
