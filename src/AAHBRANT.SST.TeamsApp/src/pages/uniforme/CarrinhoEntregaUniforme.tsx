import { Button, StatusChip, designTokens, tokensUi } from '@ui';
import { Cart24Regular, Delete24Regular, Print24Regular, CheckmarkCircle24Filled } from '@fluentui/react-icons';
import { type CatalogoUniforme, type EstoqueUniformePorObra } from '../../lib/api';
import { FotoCatalogoUniforme } from './FotoCatalogoUniforme';
import { type ItemCarrinhoUniforme } from './SeletorItensUniforme';

interface CarrinhoEntregaUniformeProps {
  carrinho: ItemCarrinhoUniforme[];
  pecas: CatalogoUniforme[];
  carregando: boolean;
  saldos: EstoqueUniformePorObra[];
  aoRemover: (catalogoUniformeId: string) => void;
  aoImprimirRascunho: () => void;
  aoConfirmar: () => void;
}

// Carrinho lateral idêntico ao de CarrinhoEntregaEpi.tsx, trocando CA/validade por tamanho.
export function CarrinhoEntregaUniforme({ carrinho, pecas, carregando, saldos, aoRemover, aoImprimirRascunho, aoConfirmar }: CarrinhoEntregaUniformeProps) {
  function peca(id: string) {
    return pecas.find((p) => p.id === id);
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
          Nenhuma peça adicionada ainda. Escolha as peças ao lado e clique em "Adicionar".
        </div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.sm }}>
          {carrinho.map((item) => {
            const p = peca(item.catalogoUniformeId);
            return (
              <div
                key={item.catalogoUniformeId}
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
                <FotoCatalogoUniforme catalogoUniformeId={item.catalogoUniformeId} temFoto={p?.temFoto ?? false} referenciaVisual={p?.referenciaVisual} tamanho={48} />
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontWeight: 700, fontSize: 13 }}>{p?.nome ?? item.catalogoUniformeId}</div>
                  <div style={{ fontSize: 11, color: designTokens.colorNeutralMedium }}>Tamanho: {item.tamanho}</div>
                  <div style={{ fontSize: 12, color: designTokens.colorPrimary, fontWeight: 600 }}>Qtd: {item.quantidade} de {saldos.find(s => s.catalogoUniformeId === item.catalogoUniformeId && s.tamanho === item.tamanho)?.saldo ?? 0} disponíveis</div>
                </div>
                <Button
                  appearance="subtle"
                  size="small"
                  icon={<Delete24Regular />}
                  onClick={() => aoRemover(item.catalogoUniformeId)}
                  aria-label={`Remover ${p?.nome ?? 'item'} do carrinho`}
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
        <Button appearance="primary" icon={<CheckmarkCircle24Filled />} onClick={aoConfirmar} disabled={carrinho.length === 0 || carregando || carrinho.some(i => i.quantidade > (saldos.find(s => s.catalogoUniformeId === i.catalogoUniformeId && s.tamanho === i.tamanho)?.saldo ?? 0))}>
          Confirmar e assinar entrega
        </Button>
      </div>
    </div>
  );
}
