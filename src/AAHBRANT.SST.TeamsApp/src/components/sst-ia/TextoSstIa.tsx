import { Fragment, type ReactNode } from 'react';

// Formatação mínima das respostas do agente: negrito (**x**), listas (- x / 1. x) e parágrafos.
// O React escapa todo o texto; nada de HTML vindo da IA é interpretado.

function negritos(linha: string): ReactNode[] {
  return linha.split(/(\*\*[^*]+\*\*)/g).map((parte, i) =>
    parte.startsWith('**') && parte.endsWith('**') && parte.length > 4 ? (
      <strong key={i}>{parte.slice(2, -2)}</strong>
    ) : (
      <Fragment key={i}>{parte.replace(/(^|\s)\*(\S[^*]*\S|\S)\*(?=\s|$|[.,;:])/g, '$1$2')}</Fragment>
    ),
  );
}

export function TextoSstIa({ texto }: { texto: string }) {
  const blocos: ReactNode[] = [];
  let lista: { ordenada: boolean; itens: string[] } | null = null;
  let paragrafo: string[] = [];

  const fecharParagrafo = () => {
    if (paragrafo.length) {
      blocos.push(
        <p key={`p${blocos.length}`} style={{ margin: '0 0 6px' }}>
          {paragrafo.map((l, i) => (
            <Fragment key={i}>
              {i > 0 && <br />}
              {negritos(l)}
            </Fragment>
          ))}
        </p>,
      );
      paragrafo = [];
    }
  };
  const fecharLista = () => {
    if (lista) {
      const Tag = lista.ordenada ? 'ol' : 'ul';
      blocos.push(
        <Tag key={`l${blocos.length}`} style={{ margin: '0 0 6px', paddingLeft: '20px' }}>
          {lista.itens.map((item, i) => (
            <li key={i} style={{ marginBottom: '2px' }}>
              {negritos(item)}
            </li>
          ))}
        </Tag>,
      );
      lista = null;
    }
  };

  for (const bruta of texto.split('\n')) {
    const linha = bruta.trimEnd();
    const item = /^\s*(?:[-*•]|(\d+)[.)])\s+(.*)$/.exec(linha);
    if (item) {
      fecharParagrafo();
      const ordenada = item[1] !== undefined;
      if (!lista || lista.ordenada !== ordenada) {
        fecharLista();
        lista = { ordenada, itens: [] };
      }
      lista.itens.push(item[2]);
    } else if (!linha.trim()) {
      fecharParagrafo();
      fecharLista();
    } else {
      fecharLista();
      paragrafo.push(linha.replace(/^#{1,6}\s+/, ''));
    }
  }
  fecharParagrafo();
  fecharLista();
  return <>{blocos}</>;
}
