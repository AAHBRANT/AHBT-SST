import { useEffect, useRef, useState } from 'react';
import { Carregando } from '@ui';
import { api } from '../../lib/api';
import { useEstilosAlertas } from './estilosAlertas';

interface Resultado {
  id: string;
  url: string | null;
}

// Foto da ocorrência: só baixa quando o cartão chega perto da tela (200 fotos de uma vez pesariam
// na rede e na memória) e revoga a object URL ao sair. A foto mostra trabalhadores, então o
// navegador não a guarda: cada exibição é um download autenticado.
export function FotoOcorrenciaEpi({ id, alt }: { id: string; alt: string }) {
  const estilos = useEstilosAlertas();
  const caixaRef = useRef<HTMLDivElement>(null);
  const [visivel, setVisivel] = useState(() => typeof IntersectionObserver === 'undefined');
  // Resultado do último download concluído; enquanto o id dele não for o atual, a foto está carregando.
  const [resultado, setResultado] = useState<Resultado | null>(null);

  useEffect(() => {
    if (visivel) return undefined;
    const caixa = caixaRef.current;
    if (!caixa) return undefined;
    const observador = new IntersectionObserver(
      (entradas) => {
        if (entradas.some((e) => e.isIntersecting)) {
          setVisivel(true);
          observador.disconnect();
        }
      },
      { rootMargin: '200px' },
    );
    observador.observe(caixa);
    return () => observador.disconnect();
  }, [visivel]);

  useEffect(() => {
    if (!visivel) return undefined;
    let cancelado = false;
    let urlCriada: string | null = null;
    api.cameras
      .baixarFotoOcorrencia(id)
      .then((blob) => {
        if (cancelado) return;
        urlCriada = URL.createObjectURL(blob);
        setResultado({ id, url: urlCriada });
      })
      .catch(() => {
        if (!cancelado) setResultado({ id, url: null });
      });
    return () => {
      cancelado = true;
      if (urlCriada) URL.revokeObjectURL(urlCriada);
    };
  }, [id, visivel]);

  const concluido = resultado?.id === id;
  const url = concluido ? resultado.url : null;
  const carregando = visivel && !concluido;
  const erro = concluido && !url;

  return (
    <div ref={caixaRef} className={estilos.fotoCaixa}>
      {url && <img src={url} alt={alt} className={estilos.fotoImg} />}
      {carregando && <Carregando variante="detalhe" linhas={1} />}
      {erro && <span>Foto indisponível</span>}
    </div>
  );
}
