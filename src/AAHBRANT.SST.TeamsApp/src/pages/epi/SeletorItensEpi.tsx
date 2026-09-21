import { useState } from 'react';
import { Button, Input, CampoData, designTokens, tokensUi } from '@ui';
import { AddCircle24Regular, CheckmarkCircle24Filled, Delete24Regular } from '@fluentui/react-icons';
import { type CatalogoEpi } from '../../lib/api';
import { FotoCatalogoEpi } from './FotoCatalogoEpi';

export interface ItemCarrinhoEpi {
  catalogoEpiId: string;
  quantidade: number;
  dataValidade: string;
}

interface LinhaEpiSelecionavelProps {
  epi: CatalogoEpi;
  itemNoCarrinho: ItemCarrinhoEpi | undefined;
  aoAdicionar: (item: ItemCarrinhoEpi) => void;
  aoRemover: () => void;
}

// Uma linha por EPI da matriz da função — o usuário ajusta quantidade/validade e adiciona ao
// carrinho; depois de adicionado, a linha mostra "No carrinho" e um botão de remover em vez do
// formulário (evita adicionar o mesmo EPI duas vezes com valores divergentes).
function LinhaEpiSelecionavel({ epi, itemNoCarrinho, aoAdicionar, aoRemover }: LinhaEpiSelecionavelProps) {
  const [quantidade, setQuantidade] = useState('1');
  const [dataValidade, setDataValidade] = useState('');

  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: tokensUi.espaco.md,
        padding: tokensUi.espaco.md,
        border: `1px solid ${designTokens.colorCardBorder}`,
        borderRadius: tokensUi.raio.md,
        backgroundColor: itemNoCarrinho ? designTokens.colorNeutralLight : designTokens.colorSurface,
      }}
    >
      <FotoCatalogoEpi catalogoEpiId={epi.id} temFoto={epi.temFoto} tamanho={40} />

      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ fontWeight: 600, fontSize: 13 }}>{epi.nome}</div>
        <div style={{ fontSize: 12, color: designTokens.colorNeutralMedium }}>
          {epi.certificadoAprovacaoNumero ? `CA: ${epi.certificadoAprovacaoNumero}` : 'Sem CA cadastrado'}
          {' · '}Estoque: {epi.saldoTotal}
        </div>
      </div>

      {itemNoCarrinho ? (
        <>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6, color: designTokens.colorSuccess, fontSize: 12, fontWeight: 600 }}>
            <CheckmarkCircle24Filled fontSize={16} />
            No carrinho · Qtd {itemNoCarrinho.quantidade}
          </div>
          <Button appearance="subtle" size="small" icon={<Delete24Regular />} onClick={aoRemover} aria-label={`Remover ${epi.nome} do carrinho`} />
        </>
      ) : (
        <>
          <Input
            type="number"
            value={quantidade}
            onChange={(_, d) => setQuantidade(d.value)}
            style={{ width: 64 }}
            aria-label={`Quantidade de ${epi.nome}`}
          />
          <CampoData
            value={dataValidade}
            onChange={(_, d) => setDataValidade(d.value)}
            aria-label={`Validade de ${epi.nome}`}
            style={{ width: 140 }}
          />
          <Button
            appearance="secondary"
            size="small"
            icon={<AddCircle24Regular />}
            onClick={() => {
              const qtd = Number(quantidade);
              if (!qtd || qtd < 1) return;
              aoAdicionar({ catalogoEpiId: epi.id, quantidade: qtd, dataValidade });
            }}
          >
            Adicionar
          </Button>
        </>
      )}
    </div>
  );
}

interface SeletorItensEpiProps {
  episPermitidos: CatalogoEpi[];
  carrinho: ItemCarrinhoEpi[];
  aoAdicionar: (item: ItemCarrinhoEpi) => void;
  aoRemover: (catalogoEpiId: string) => void;
}

// Lista completa dos EPIs vinculados à função do funcionário selecionado (matriz EPI x Função, já
// carregada em episPermitidos) — pedido do usuário: em toda entrega, primeira ou não, aparecem
// todos os itens da função para escolher quais entregar desta vez, em vez de escolher um por um
// num dropdown.
export function SeletorItensEpi({ episPermitidos, carrinho, aoAdicionar, aoRemover }: SeletorItensEpiProps) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.sm }}>
      {episPermitidos.map((epi) => (
        <LinhaEpiSelecionavel
          key={epi.id}
          epi={epi}
          itemNoCarrinho={carrinho.find((i) => i.catalogoEpiId === epi.id)}
          aoAdicionar={aoAdicionar}
          aoRemover={() => aoRemover(epi.id)}
        />
      ))}
    </div>
  );
}
