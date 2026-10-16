import { useRef, useState, type DragEvent } from 'react';
import { makeStyles, tokens } from '@fluentui/react-components';
import { Button, Spinner, Text } from '@ui';
import { CheckmarkCircle24Regular, DocumentPdf24Regular, Dismiss24Regular } from '@fluentui/react-icons';
import type { Obra } from '../../lib/api';

const useStyles = makeStyles({
  area: {
    display: 'flex',
    alignItems: 'center',
    gap: '14px',
    flexWrap: 'wrap',
    padding: '16px',
    borderRadius: tokens.borderRadiusLarge,
    border: `2px dashed ${tokens.colorBrandStroke1}`,
    backgroundColor: tokens.colorNeutralBackground2,
    cursor: 'pointer',
  },
  arrastando: { backgroundColor: tokens.colorBrandBackground2 },
  icone: { color: tokens.colorBrandForeground1, flexShrink: 0 },
  texto: { display: 'flex', flexDirection: 'column', minWidth: 0, flex: 1 },
  status: { display: 'flex', alignItems: 'center', gap: '6px', fontWeight: tokens.fontWeightSemibold },
  ok: { color: tokens.colorPaletteGreenForeground1 },
});

// Área de envio do PDF no cadastro do PGR/PCMSO (16/10/2026): ao escolher o arquivo, a IA lê a
// identificação do documento e a tela preenche os campos para o técnico conferir.
export function EnvioPdfComIa({
  arquivo,
  lendo,
  lido,
  aoEscolher,
  aoRemover,
}: {
  arquivo: File | null;
  lendo: boolean;
  lido: boolean;
  aoEscolher: (arquivo: File) => void;
  aoRemover: () => void;
}) {
  const styles = useStyles();
  const input = useRef<HTMLInputElement>(null);
  const [arrastando, setArrastando] = useState(false);

  function escolher(lista: FileList | null) {
    const f = lista?.[0];
    if (f) aoEscolher(f);
  }

  function soltar(e: DragEvent) {
    e.preventDefault();
    setArrastando(false);
    escolher(e.dataTransfer.files);
  }

  return (
    <div
      className={`${styles.area} ${arrastando ? styles.arrastando : ''}`}
      onClick={() => !lendo && input.current?.click()}
      onDragOver={(e) => {
        e.preventDefault();
        setArrastando(true);
      }}
      onDragLeave={() => setArrastando(false)}
      onDrop={soltar}
      role="button"
      tabIndex={0}
    >
      <DocumentPdf24Regular className={styles.icone} />
      <div className={styles.texto}>
        <Text weight="semibold">{arquivo ? arquivo.name : 'Envie o PDF para a IA preencher os campos'}</Text>
        <Text size={200}>
          {arquivo ? 'O PDF também fica salvo no documento ao adicionar.' : 'Arraste o arquivo ou clique para escolher. Sem PDF, preencha manualmente.'}
        </Text>
      </div>
      {lendo && (
        <span className={styles.status}>
          <Spinner size="tiny" /> Lendo com IA…
        </span>
      )}
      {!lendo && lido && (
        <span className={`${styles.status} ${styles.ok}`}>
          <CheckmarkCircle24Regular /> Lido pela IA
        </span>
      )}
      {arquivo && !lendo && (
        <Button
          appearance="subtle"
          icon={<Dismiss24Regular />}
          aria-label="Remover PDF"
          onClick={(e) => {
            e.stopPropagation();
            if (input.current) input.current.value = '';
            aoRemover();
          }}
        />
      )}
      <input
        ref={input}
        type="file"
        accept="application/pdf"
        style={{ display: 'none' }}
        onChange={(e) => escolher(e.target.files)}
      />
    </div>
  );
}

function normalizar(s: string) {
  return s
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9 ]/g, ' ')
    .split(' ')
    .filter((p) => p.length > 2 && !['consorcio', 'obra', 'fase', 'ltda', 'para', 'das', 'dos'].includes(p));
}

// Obra cadastrada com mais palavras em comum com o nome escrito no PDF (nenhuma em comum = sem sugestão).
export function sugerirObra(textoDocumento: string | null, obras: Obra[]): string | null {
  if (!textoDocumento) return null;
  const doc = new Set(normalizar(textoDocumento));
  let melhor: { id: string; pontos: number } | null = null;
  for (const o of obras) {
    const pontos = normalizar(o.nome).filter((p) => doc.has(p)).length;
    if (pontos > 0 && (!melhor || pontos > melhor.pontos)) melhor = { id: o.id, pontos };
  }
  return melhor?.id ?? null;
}

// "2026-03-01T00:00:00Z" → "2026-03-01" (formato do CampoData).
export function dataCampo(iso: string | null): string {
  return iso ? iso.slice(0, 10) : '';
}

export const DICA_IA = 'Preenchido pela IA — confira';
