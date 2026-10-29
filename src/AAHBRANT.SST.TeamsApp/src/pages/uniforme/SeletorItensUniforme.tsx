import { useState } from 'react';
import { Button, Input, designTokens, tokensUi } from '@ui';
import { AddCircle24Regular, CheckmarkCircle24Filled, Delete24Regular } from '@fluentui/react-icons';
import { type CatalogoUniforme, type TamanhoUniformeTrabalhador } from '../../lib/api';
import { FotoCatalogoUniforme } from './FotoCatalogoUniforme';

export interface ItemCarrinhoUniforme {
  catalogoUniformeId: string;
  quantidade: number;
  tamanho: string;
}

interface LinhaUniformeSelecionavelProps {
  peca: CatalogoUniforme;
  tamanho: string | null;
  itemNoCarrinho: ItemCarrinhoUniforme | undefined;
  aoAdicionar: (item: ItemCarrinhoUniforme) => void;
  aoRemover: () => void;
  aoCadastrarTamanho: () => void;
}

// Uma linha por peça da matriz da função — mesma mecânica de SeletorItensEpi.tsx. O tamanho nunca é
// digitado: vem do cadastro do trabalhador, e peça sem tamanho não entra no carrinho (o servidor
// também recusa em CriarEntregaUniformeCommand).
function LinhaUniformeSelecionavel({ peca, tamanho, itemNoCarrinho, aoAdicionar, aoRemover, aoCadastrarTamanho }: LinhaUniformeSelecionavelProps) {
  const [quantidade, setQuantidade] = useState('1');

  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: tokensUi.espaco.md,
        padding: tokensUi.espaco.md,
        border: `1px solid ${designTokens.colorCardBorder}`,
        borderRadius: tokensUi.raio.md,
        backgroundColor: itemNoCarrinho ? designTokens.colorNeutralLight : designTokens.colorSurface,
      }}
    >
      <FotoCatalogoUniforme catalogoUniformeId={peca.id} temFoto={peca.temFoto} tamanho={40} />

      <div style={{ flex: 1, minWidth: 140 }}>
        <div style={{ fontWeight: 600, fontSize: 13 }}>{peca.nome}</div>
        <div style={{ fontSize: 12, color: designTokens.colorNeutralMedium }}>
          Tamanho: <b style={{ color: designTokens.colorPrimary }}>{tamanho ?? '—'}</b>
        </div>
      </div>

      {tamanho === null ? (
        <>
          <span style={{ fontSize: 12, fontWeight: 600, color: designTokens.colorWarning }}>Sem tamanho cadastrado</span>
          <Button appearance="secondary" size="small" onClick={aoCadastrarTamanho}>
            Cadastrar em Tamanhos
          </Button>
        </>
      ) : itemNoCarrinho ? (
        <>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6, color: designTokens.colorSuccess, fontSize: 12, fontWeight: 600 }}>
            <CheckmarkCircle24Filled fontSize={16} />
            No carrinho · Qtd {itemNoCarrinho.quantidade}
          </div>
          <Button appearance="subtle" size="small" icon={<Delete24Regular />} onClick={aoRemover} aria-label={`Remover ${peca.nome} do carrinho`} />
        </>
      ) : (
        <>
          <Input
            type="number"
            value={quantidade}
            onChange={(_, d) => setQuantidade(d.value)}
            style={{ width: 64 }}
            aria-label={`Quantidade de ${peca.nome}`}
          />
          <Button
            appearance="secondary"
            size="small"
            icon={<AddCircle24Regular />}
            onClick={() => {
              const qtd = Number(quantidade);
              if (!qtd || qtd < 1) return;
              aoAdicionar({ catalogoUniformeId: peca.id, quantidade: qtd, tamanho });
            }}
          >
            Adicionar
          </Button>
        </>
      )}
    </div>
  );
}

interface SeletorItensUniformeProps {
  pecasPermitidas: CatalogoUniforme[];
  tamanhos: TamanhoUniformeTrabalhador[];
  carrinho: ItemCarrinhoUniforme[];
  aoAdicionar: (item: ItemCarrinhoUniforme) => void;
  aoRemover: (catalogoUniformeId: string) => void;
  aoCadastrarTamanho: () => void;
}

// Lista completa das peças vinculadas à função do funcionário (matriz Uniforme x Função).
export function SeletorItensUniforme({ pecasPermitidas, tamanhos, carrinho, aoAdicionar, aoRemover, aoCadastrarTamanho }: SeletorItensUniformeProps) {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.sm }}>
      {pecasPermitidas.map((peca) => (
        <LinhaUniformeSelecionavel
          key={peca.id}
          peca={peca}
          tamanho={tamanhos.find((t) => t.catalogoUniformeId === peca.id)?.tamanho ?? null}
          itemNoCarrinho={carrinho.find((i) => i.catalogoUniformeId === peca.id)}
          aoAdicionar={aoAdicionar}
          aoRemover={() => aoRemover(peca.id)}
          aoCadastrarTamanho={aoCadastrarTamanho}
        />
      ))}
    </div>
  );
}
