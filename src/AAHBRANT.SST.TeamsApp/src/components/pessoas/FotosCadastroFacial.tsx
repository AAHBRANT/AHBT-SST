import { useEffect, useState } from 'react';
import { Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle } from '@fluentui/react-components';
import { Button, Legenda, Text } from '@ui';
import { api, type FotoCadastroFacial } from '../../lib/api';

interface FotosCadastroFacialProps {
  trabalhadorId: string;
  /** Muda a cada novo cadastro facial para recarregar a lista. */
  versao: number;
}

function formatarData(iso: string): string {
  return new Date(iso).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
}

function useUrlDaFoto(trabalhadorId: string, fotoId: string | null): string | null {
  const [url, setUrl] = useState<string | null>(null);

  useEffect(() => {
    if (!fotoId) {
      setUrl(null);
      return undefined;
    }
    let cancelado = false;
    let criada: string | null = null;
    api.trabalhadores
      .baixarFotoCadastroFacial(trabalhadorId, fotoId)
      .then((blob) => {
        if (cancelado) return;
        criada = URL.createObjectURL(blob);
        setUrl(criada);
      })
      .catch(() => {
        if (!cancelado) setUrl(null);
      });
    return () => {
      cancelado = true;
      if (criada) URL.revokeObjectURL(criada);
    };
  }, [trabalhadorId, fotoId]);

  return url;
}

function Miniatura({ trabalhadorId, foto, aoAbrir }: { trabalhadorId: string; foto: FotoCadastroFacial; aoAbrir: () => void }) {
  const url = useUrlDaFoto(trabalhadorId, foto.id);
  return (
    <button
      type="button"
      onClick={aoAbrir}
      aria-label={`Ampliar foto do cadastro facial de ${formatarData(foto.capturadaEm)}`}
      style={{
        border: '1px solid var(--colorNeutralStroke1)',
        borderRadius: 6,
        padding: 0,
        background: 'var(--colorNeutralBackground3)',
        width: 120,
        cursor: 'pointer',
        overflow: 'hidden',
        textAlign: 'left',
      }}
    >
      <div style={{ width: 120, height: 120, background: '#eee', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
        {url ? <img src={url} alt="" style={{ width: '100%', height: '100%', objectFit: 'cover' }} /> : <Legenda>Carregando…</Legenda>}
      </div>
      <div style={{ padding: '6px 8px' }}>
        <Legenda>{formatarData(foto.capturadaEm)}</Legenda>
      </div>
    </button>
  );
}

// Galeria das fotos usadas no cadastro facial, guardadas no perfil do funcionário (a mais recente
// primeiro). Só entram aqui fotos aprovadas na validação de qualidade.
export function FotosCadastroFacial({ trabalhadorId, versao }: FotosCadastroFacialProps) {
  const [fotos, setFotos] = useState<FotoCadastroFacial[] | null>(null);
  const [erro, setErro] = useState(false);
  const [ampliada, setAmpliada] = useState<FotoCadastroFacial | null>(null);
  const urlAmpliada = useUrlDaFoto(trabalhadorId, ampliada?.id ?? null);

  useEffect(() => {
    let cancelado = false;
    setErro(false);
    api.trabalhadores
      .listarFotosCadastroFacial(trabalhadorId)
      .then((lista) => !cancelado && setFotos(lista))
      .catch(() => !cancelado && setErro(true));
    return () => {
      cancelado = true;
    };
  }, [trabalhadorId, versao]);

  if (erro) return <Legenda>Não foi possível carregar as fotos do cadastro facial.</Legenda>;
  if (fotos === null) return <Legenda>Carregando fotos do cadastro facial…</Legenda>;
  if (fotos.length === 0) return <Legenda>Nenhuma foto de cadastro facial guardada neste perfil.</Legenda>;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      <Text weight="semibold">Fotos do cadastro facial ({fotos.length})</Text>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12 }}>
        {fotos.map((foto) => (
          <Miniatura key={foto.id} trabalhadorId={trabalhadorId} foto={foto} aoAbrir={() => setAmpliada(foto)} />
        ))}
      </div>
      <Dialog open={ampliada !== null} onOpenChange={(_, d) => !d.open && setAmpliada(null)}>
        <DialogSurface style={{ maxWidth: 560 }}>
          <DialogBody>
            <DialogTitle>Foto do cadastro facial{ampliada ? ` — ${formatarData(ampliada.capturadaEm)}` : ''}</DialogTitle>
            <DialogContent style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              {urlAmpliada ? (
                <img src={urlAmpliada} alt="Foto do cadastro facial" style={{ maxWidth: '100%', maxHeight: '60vh', objectFit: 'contain' }} />
              ) : (
                <Legenda>Carregando…</Legenda>
              )}
              {ampliada && <Legenda>SHA-256: {ampliada.hashSha256}</Legenda>}
            </DialogContent>
            <DialogActions>
              <Button appearance="primary" onClick={() => setAmpliada(null)}>
                Fechar
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </div>
  );
}
