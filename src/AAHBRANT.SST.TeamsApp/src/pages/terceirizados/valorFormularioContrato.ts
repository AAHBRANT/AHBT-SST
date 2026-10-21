import type { DadosContrato } from '../../lib/api';

// Valor do FormularioContrato e conversão para o corpo da API — separados do componente para o
// Fast Refresh do Vite (arquivo de componente só exporta componente).

export interface ValorFormularioContrato {
  obraId: string;
  numeroContrato: string;
  dataInicioVigencia: string;
  dataFimVigencia: string;
  vagas: { funcaoId: string; quantidade: string }[];
}

export const formularioContratoVazio: ValorFormularioContrato = {
  obraId: '',
  numeroContrato: '',
  dataInicioVigencia: '',
  dataFimVigencia: '',
  vagas: [{ funcaoId: '', quantidade: '1' }],
};

export function paraDadosContrato(valor: ValorFormularioContrato): DadosContrato {
  return {
    numeroContrato: valor.numeroContrato.trim(),
    dataInicioVigencia: valor.dataInicioVigencia,
    dataFimVigencia: valor.dataFimVigencia,
    vagas: valor.vagas.map((v) => ({ funcaoId: v.funcaoId, quantidade: Number(v.quantidade) || 0 })),
  };
}
