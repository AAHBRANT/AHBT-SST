import { useEffect, useState } from 'react';
import { Card, FeedbackInline, designTokens, tokensUi, useTipografia } from '@ui';
import { api, type CatalogoUniforme } from '../../lib/api';

// Tamanho de uniforme por trabalhador — seção do perfil (docs/superpowers/specs/2026-09-07-modulo-
// uniforme-design.md: "nova seção na tela de perfil dele, ao lado das demais seções de dados
// cadastrais"). Substitui a aba global TamanhosUniformeTab.tsx (removida na revisão final do
// branch, que apontou o desvio da spec) — mesma API, sem o seletor de trabalhador, que aqui vem da
// própria página de perfil. Camada ui/: como é seção de página (não página inteira), o título vai
// no Card, não num PageHeader.
export function TamanhosUniformeSecao({ trabalhadorId }: { trabalhadorId: string }) {
  const tipo = useTipografia();
  const [itensCatalogo, setItensCatalogo] = useState<CatalogoUniforme[]>([]);
  const [tamanhos, setTamanhos] = useState<Record<string, string>>({});
  const [erro, setErro] = useState<string | null>(null);
  const [carregandoLista, setCarregandoLista] = useState(true);

  async function carregar() {
    try {
      setErro(null);
      const [listaItens, listaTamanhos] = await Promise.all([
        api.catalogosUniforme.listar(),
        api.trabalhadores.listarTamanhosUniforme(trabalhadorId),
      ]);
      setItensCatalogo(listaItens);
      const mapa: Record<string, string> = {};
      for (const item of listaTamanhos) mapa[item.catalogoUniformeId] = item.tamanho;
      setTamanhos(mapa);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar tamanhos de uniforme.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trabalhadorId]);

  if (carregandoLista || itensCatalogo.length === 0) return null;

  return (
    <Card titulo="Tamanhos de uniforme" subtitulo="Vêm do cadastro no G-RH e são usados na separação de kits">
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      {/* Uma peça por quadro (Proposta 1 de cards, 21/10) no lugar da tabela de duas colunas. */}
      <div
        role="group"
        aria-label="Tamanhos de uniforme do trabalhador"
        style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(120px, 1fr))', gap: tokensUi.espaco.sm }}
      >
        {itensCatalogo.map((item) => (
          <div
            key={item.id}
            style={{
              display: 'flex',
              flexDirection: 'column',
              gap: 6,
              padding: tokensUi.espaco.sm,
              borderRadius: tokensUi.raio.md,
              border: `1px solid ${designTokens.colorCardBorder}`,
              backgroundColor: designTokens.colorNeutralLight,
            }}
          >
            <span className={tipo.micro} style={{ color: designTokens.colorNeutralMedium }}>
              {item.nome}
            </span>
            <strong>{tamanhos[item.id] || 'Não informado'}</strong>
          </div>
        ))}
      </div>

    </Card>
  );
}
