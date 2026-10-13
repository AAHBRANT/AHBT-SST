import { useEffect, useMemo, useState } from 'react';
import { api, type TipoRegistroTravavel } from '../lib/api';

// Registros já travados pela assinatura (12/10): a tela esconde editar/excluir deles. O servidor também
// recusa a alteração, então isto é só para não oferecer um botão que vai dar erro. Se a consulta falhar, nada
// fica escondido (o servidor continua barrando).
export function useRegistrosTravados(entidadeTipo: TipoRegistroTravavel, ids: readonly string[]) {
  const chave = useMemo(() => [...new Set(ids)].sort().join(','), [ids]);
  const [travados, setTravados] = useState<ReadonlySet<string>>(new Set());

  useEffect(() => {
    let cancelado = false;
    const lista = chave ? chave.split(',') : [];
    api.assinatura
      .listarRegistrosTravados(entidadeTipo, lista)
      .then((r) => !cancelado && setTravados(new Set(r)))
      .catch(() => !cancelado && setTravados(new Set()));
    return () => {
      cancelado = true;
    };
  }, [entidadeTipo, chave]);

  return travados;
}
