import { useEffect, useState } from 'react';
import { Avatar } from '@ui';
import { api } from '../lib/api';

// Foto do cadastro do funcionário em listas (pedido do usuário, 25/11/2026: as Entregas de EPI
// mostravam só as iniciais). Uma lista pode ter centenas de linhas, então:
// - cada funcionário é baixado uma vez só (uma promessa por id), mesmo aparecendo em várias linhas
//   (várias entregas), e a foto fica valendo para qualquer tela durante a sessão;
// - os downloads entram numa fila de no máximo 4 por vez, na ordem em que as linhas aparecem
//   (as de cima primeiro), para não disparar centenas de requisições de uma vez.
// Não usa IntersectionObserver: em alguns navegadores embutidos ele não dispara e a foto nunca vinha.
// Sem foto cadastrada (ou se o download falhar), mostra as iniciais no Avatar do Fluent.
const MAXIMO_SIMULTANEOS = 4;
const cache = new Map<string, Promise<string | null>>();
const fila: Array<() => void> = [];
let emAndamento = 0;

function proximo() {
  while (emAndamento < MAXIMO_SIMULTANEOS && fila.length > 0) {
    emAndamento += 1;
    fila.shift()!();
  }
}

function obterFoto(trabalhadorId: string): Promise<string | null> {
  let p = cache.get(trabalhadorId);
  if (!p) {
    p = new Promise<string | null>((resolver) => {
      fila.push(() => {
        api.trabalhadores
          .baixarFoto(trabalhadorId)
          .then((blob) => resolver(URL.createObjectURL(blob)))
          .catch(() => {
            cache.delete(trabalhadorId); // falha transitória: tenta de novo na próxima vez
            resolver(null);
          })
          .finally(() => {
            emAndamento -= 1;
            proximo();
          });
      });
      proximo();
    });
    cache.set(trabalhadorId, p);
  }
  return p;
}

export interface FotoTrabalhadorProps {
  trabalhadorId: string;
  nome: string;
  /** Flag do cadastro: sem foto, nem tenta baixar. */
  temFoto?: boolean;
  tamanho?: 24 | 28 | 32 | 36 | 40 | 48;
}

export function FotoTrabalhador({ trabalhadorId, nome, temFoto, tamanho = 36 }: FotoTrabalhadorProps) {
  const [url, setUrl] = useState<string | null>(null);

  useEffect(() => {
    setUrl(null);
    if (!temFoto || !trabalhadorId) return;
    let cancelado = false;
    void obterFoto(trabalhadorId).then((u) => { if (!cancelado) setUrl(u); });
    return () => { cancelado = true; };
  }, [trabalhadorId, temFoto]);

  return url ? (
    <Avatar image={{ src: url, alt: '' }} name={nome} size={tamanho} />
  ) : (
    <Avatar name={nome} color="colorful" size={tamanho} />
  );
}
