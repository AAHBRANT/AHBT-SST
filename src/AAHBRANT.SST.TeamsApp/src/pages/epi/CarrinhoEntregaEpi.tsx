import { Button, StatusChip, designTokens, tokensUi } from '@ui';
import { Cart24Regular, Delete24Regular, Print24Regular, CheckmarkCircle24Filled } from '@fluentui/react-icons';
import { type CatalogoEpi } from '../../lib/api';
import { FotoCatalogoEpi } from './FotoCatalogoEpi';
import { type ItemCarrinhoEpi } from './SeletorItensEpi';

interface CarrinhoEntregaEpiProps {
  carrinho: ItemCarrinhoEpi[];
  epis: CatalogoEpi[];
  carregando: boolean;
  aoRemover: (catalogoEpiId: string) => void;
  aoImprimirRascunho: () => void;
  aoConfirmar: () => void;
}

// Carrinho lateral ("Cesta do Trabalhador") — junta os itens escolhidos em SeletorItensEpi antes de
// confirmar a entrega, seguindo o mockup fornecido pelo usuário (imagem "Entrega de EPI com
// Carrinho Lateral"), com a paleta institucional da AAHBRANT no lugar das cores genéricas do
// mockup. Fica ao lado do formulário dentro do mesmo PainelCriacaoInline (ver EntregasTab.tsx).
export function CarrinhoEntregaEpi({ carrinho, epis, carregando, aoRemover, aoImprimirRascunho, aoConfirmar }: CarrinhoEntregaEpiProps) {
  function epi(id: string) {
    return epis.find((e) => e.id === id);
  }

  return (
    <div
      style={{
        border: `1px solid ${designTokens.colorCardBorder}`,
        borderRadius: tokensUi.raio.lg,
        backgroundColor: designTokens.colorSurface,
        padding: tokensUi.espaco.lg,
        position: 'sticky',
        top: tokensUi.espaco.lg,
        display: 'flex',
        flexDirection: 'column',
        gap: tokensUi.espaco.md,
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontWeight: 700, fontSize: 14 }}>
          <Cart24Regular />
          Itens a entregar
        </div>
        <StatusChip tom={carrinho.length > 0 ? 'info' : 'neutro'}>{carrinho.length} {carrinho.length === 1 ? 'item' : 'itens'}</StatusChip>
      </div>

      {carrinho.length === 0 ? (
        <div style={{ fontSize: 13, color: designTokens.colorNeutralMedium, padding: `${tokensUi.espaco.md} 0` }}>
          Nenhum item adicionado ainda. Escolha os EPIs ao lado e clique em "Adicionar".
        </div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.sm }}>
          {carrinho.map((item) => {
            const e = epi(item.catalogoEpiId);
            return (
              <div
                key={item.catalogoEpiId}
                style={{
                  display: 'flex',
                  gap: tokensUi.espaco.sm,
                  padding: tokensUi.espaco.sm,
                  border: `1px solid ${designTokens.colorCardBorder}`,
                  borderRadius: tokensUi.raio.md,
                  backgroundColor: designTokens.colorNeutralLight,
                  position: 'relative',
                }}
              >
                <FotoCatalogoEpi catalogoEpiId={item.catalogoEpiId} temFoto={e?.temFoto ?? false} tamanho={48} />
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontWeight: 700, fontSize: 13 }}>{e?.nome ?? item.catalogoEpiId}</div>
                  <div style={{ fontSize: 11, color: designTokens.colorNeutralMedium }}>
                    {e?.certificadoAprovacaoNumero ? `CA: ${e.certificadoAprovacaoNumero}` : 'Sem CA cadastrado'}
                  </div>
                  <div style={{ fontSize: 12, color: designTokens.colorPrimary, fontWeight: 600 }}>Qtd: {item.quantidade}</div>
                </div>
                <Button
                  appearance="subtle"
                  size="small"
                  icon={<Delete24Regular />}
                  onClick={() => aoRemover(item.catalogoEpiId)}
                  aria-label={`Remover ${e?.nome ?? 'item'} do carrinho`}
                  style={{ position: 'absolute', top: 4, right: 4 }}
                />
              </div>
            );
          })}
        </div>
      )}

      <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginTop: 4, paddingTop: tokensUi.espaco.md, borderTop: `1px solid ${tokensUi.bordaSuave}` }}>
        <Button appearance="secondary" icon={<Print24Regular />} onClick={aoImprimirRascunho} disabled={carrinho.length === 0}>
          Imprimir canhoto de recibo
        </Button>
        <Button appearance="primary" icon={<CheckmarkCircle24Filled />} onClick={aoConfirmar} disabled={carrinho.length === 0 || carregando}>
          Confirmar entrega
        </Button>
      </div>
    </div>
  );
}
