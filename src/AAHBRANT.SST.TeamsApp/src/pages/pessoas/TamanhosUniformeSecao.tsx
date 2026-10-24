import { useEffect, useState } from 'react';
import { Button, Card, FeedbackInline, FormRodape, Input, designTokens, tokensUi, useTipografia } from '@ui';
import { Save24Regular } from '@fluentui/react-icons';
import { api, type CatalogoUniforme, type ItemTamanhoUniforme } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

// Tamanho de uniforme por trabalhador — seção do perfil (docs/superpowers/specs/2026-09-07-modulo-
// uniforme-design.md: "nova seção na tela de perfil dele, ao lado das demais seções de dados
// cadastrais"). Substitui a aba global TamanhosUniformeTab.tsx (removida na revisão final do
// branch, que apontou o desvio da spec) — mesma API, sem o seletor de trabalhador, que aqui vem da
// própria página de perfil. Camada ui/: como é seção de página (não página inteira), o título vai
// no Card, não num PageHeader.
export function TamanhosUniformeSecao({ trabalhadorId }: { trabalhadorId: string }) {
  const sucessoToast = useSucessoToast();
  const tipo = useTipografia();
  const [itensCatalogo, setItensCatalogo] = useState<CatalogoUniforme[]>([]);
  const [tamanhos, setTamanhos] = useState<Record<string, string>>({});
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
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

  async function salvar() {
    try {
      setCarregando(true);
      setErro(null);
      const itens: ItemTamanhoUniforme[] = Object.entries(tamanhos)
        .filter(([, tamanho]) => tamanho.trim() !== '')
        .map(([catalogoUniformeId, tamanho]) => ({ catalogoUniformeId, tamanho: tamanho.trim() }));
      await api.trabalhadores.definirTamanhosUniforme(trabalhadorId, itens);
      sucessoToast('Tamanhos de uniforme salvos com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar tamanhos de uniforme.');
    } finally {
      setCarregando(false);
    }
  }

  if (carregandoLista || itensCatalogo.length === 0) return null;

  return (
    <Card titulo="Tamanhos de uniforme" subtitulo="Usados na separação de kits">
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
          <label
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
            <Input
              value={tamanhos[item.id] ?? ''}
              onChange={(_, d) => setTamanhos({ ...tamanhos, [item.id]: d.value })}
              placeholder="ex.: M, 42"
              aria-label={`Tamanho de ${item.nome}`}
            />
          </label>
        ))}
      </div>

      <FormRodape>
        <Button appearance="primary" icon={<Save24Regular />} onClick={salvar} disabled={carregando}>
          Salvar tamanhos
        </Button>
      </FormRodape>
    </Card>
  );
}
