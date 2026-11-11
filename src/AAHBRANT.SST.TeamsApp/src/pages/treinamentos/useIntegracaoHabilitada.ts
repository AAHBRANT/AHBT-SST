import { useEffect, useState } from 'react';
import { api } from '../../lib/api';

let cache: boolean | null = null;
let consulta: Promise<boolean> | null = null;

// Uma consulta só, compartilhada por todas as montagens (inclusive a montagem dupla do StrictMode em dev).
function consultar(): Promise<boolean> {
  consulta ??= api.obras
    .listar()
    .then(async (obras) => {
      const obra = obras[0];
      if (!obra) return false;
      return (await api.integracoesAtendimento.entrada(obra.id)).habilitada === true;
    })
    .catch(() => false)
    .then((h) => {
      cache = h;
      // Falha de rede não fica gravada: a próxima montagem tenta de novo.
      if (!h) consulta = null;
      return h;
    });
  return consulta;
}

// Chave Integracao:AtendimentoHabilitado: a aba "Integração" só aparece com ela ligada. A chave é do
// servidor inteiro, então basta consultar a entrada de qualquer obra visível.
export function useIntegracaoHabilitada(): boolean {
  const [habilitada, setHabilitada] = useState(cache ?? false);
  useEffect(() => {
    if (cache === true) {
      setHabilitada(true);
      return;
    }
    let ativo = true;
    void consultar().then((h) => {
      if (ativo) setHabilitada(h);
    });
    return () => {
      ativo = false;
    };
  }, []);
  return habilitada;
}
