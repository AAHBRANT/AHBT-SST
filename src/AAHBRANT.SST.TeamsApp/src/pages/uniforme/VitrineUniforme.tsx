import { useState } from 'react';
import { Button, Dialog, DialogSurface, DialogBody, DialogTitle, DialogContent, DialogActions } from '@ui';
import { AddCircle24Regular, Delete24Regular } from '@fluentui/react-icons';
import type { CatalogoUniforme, EstoqueUniformePorObra, TamanhoUniformeTrabalhador } from '../../lib/api';
import { FotoCatalogoUniforme } from './FotoCatalogoUniforme';
import { referenciaUniforme } from './referenciasUniforme';
import type { ItemCarrinhoUniforme } from './SeletorItensUniforme';

function PecaUniforme({ peca, grade, tamanhoSugerido, item, habilitado, aoAdicionar, aoRemover }: {
  peca: CatalogoUniforme; grade: EstoqueUniformePorObra[]; tamanhoSugerido?: string;
  item?: ItemCarrinhoUniforme; habilitado: boolean;
  aoAdicionar: (item: ItemCarrinhoUniforme) => void; aoRemover: () => void;
}) {
  const referencia = referenciaUniforme(peca.referenciaVisual);
  const defaults = referencia?.categoria === 'Calças' || peca.nome.toUpperCase().includes('CALÇA') ? ['40', '42', '44', '46'] : ['P', 'M', 'G', 'GG'];
  const tamanhos = [...new Set([...defaults, ...grade.map(g => g.tamanho), ...(tamanhoSugerido ? [tamanhoSugerido] : [])])];
  const [escolhido, setEscolhido] = useState(item?.tamanho ?? tamanhoSugerido ?? defaults[1]);
  const tamanho = item?.tamanho ?? escolhido;
  const [quantidade, setQuantidade] = useState('1');
  const [ampliada, setAmpliada] = useState(false);
  const saldo = grade.find(g => g.tamanho === tamanho)?.saldo ?? 0;
  const qtd = Number(quantidade);
  const funcoes = peca.funcoesAtribuidas ?? [];
  const saldoDe = (t: string) => grade.find(g => g.tamanho === t)?.saldo ?? 0;
  const mudarQtd = (delta: number) => setQuantidade(String(Math.max(1, (Number.isInteger(qtd) ? qtd : 1) + delta)));
  return <article className={`uniforme-peca ${item ? 'uniforme-peca-selecionada' : ''}`}>
    <button type="button" className="uniforme-foto-botao" onClick={() => setAmpliada(true)} aria-label={`Ampliar foto de ${peca.nome}`}>
      <FotoCatalogoUniforme catalogoUniformeId={peca.id} temFoto={peca.temFoto} referenciaVisual={peca.referenciaVisual} destaque />
      {(referencia || peca.temFoto) && <span className="uniforme-foto-ampliar">Ampliar</span>}
    </button>
    <div className="uniforme-peca-corpo">
      <div>
        <strong className="uniforme-peca-nome">{peca.nome}</strong>
        <span className="uniforme-meta">{funcoes.length ? funcoes.join(' · ') : 'Nenhuma função vinculada na matriz'}</span>
        {referencia && <span className="uniforme-cor"><i style={{ background: COR_HEX[referencia.cor] ?? '#999' }} />{referencia.cor} · {referencia.tecido}</span>}
      </div>
      <div>
        <span className="uniforme-rotulo">Tamanho · saldo na obra</span>
        <div className="uniforme-tamanhos" role="radiogroup" aria-label={`Tamanho de ${peca.nome}`}>
          {tamanhos.map(t => <button type="button" key={t} role="radio" aria-checked={t === tamanho} disabled={!!item}
            className={`${t === tamanho ? 'ativo' : ''} ${saldoDe(t) <= 0 ? 'zerado' : ''}`} onClick={() => setEscolhido(t)}>
            {t}<small>{saldoDe(t)}</small></button>)}
        </div>
      </div>
      {item ? <div className="uniforme-adicionar"><span className="uniforme-meta" style={{ flex: 1 }}>No kit: {item.quantidade} · tamanho {item.tamanho}</span><Button icon={<Delete24Regular />} onClick={aoRemover}>Remover</Button></div> :
        <div className="uniforme-adicionar">
          <div className="uniforme-qtd">
            <button type="button" onClick={() => mudarQtd(-1)} aria-label="Diminuir quantidade">−</button>
            <input type="number" min={1} max={saldo} value={quantidade} onChange={e => setQuantidade(e.target.value)} aria-label={`Quantidade de ${peca.nome}`} />
            <button type="button" onClick={() => mudarQtd(1)} aria-label="Aumentar quantidade">+</button>
          </div>
          <Button appearance="primary" icon={<AddCircle24Regular />} style={{ flex: 1 }} disabled={!habilitado || !Number.isInteger(qtd) || qtd < 1 || qtd > saldo}
            title={habilitado && saldo <= 0 ? 'Sem saldo neste tamanho na obra' : undefined}
            onClick={() => aoAdicionar({ catalogoUniformeId: peca.id, quantidade: qtd, tamanho })}>Adicionar</Button>
        </div>}
    </div>
    <Dialog open={ampliada} onOpenChange={(_, d) => setAmpliada(d.open)}><DialogSurface style={{ maxWidth: 560 }}><DialogBody><DialogTitle>{peca.nome}{referencia ? ` · ${referencia.cor}` : ''}</DialogTitle><DialogContent>
      <div className="uniforme-foto-grande"><FotoCatalogoUniforme catalogoUniformeId={peca.id} temFoto={peca.temFoto} referenciaVisual={peca.referenciaVisual} destaque /></div>
    </DialogContent><DialogActions><Button onClick={() => setAmpliada(false)}>Fechar</Button></DialogActions></DialogBody></DialogSurface></Dialog>
  </article>;
}

const COR_HEX: Record<string, string> = { 'Laranja': '#f26a1b', 'Azul royal': '#1f4fd1', 'Marsala': '#670000', 'Cinza claro': '#bdbdbd' };

export function VitrineUniforme({ pecasPermitidas, tamanhos, carrinho, saldos, habilitado, aoAdicionar, aoRemover }: {
  pecasPermitidas: CatalogoUniforme[]; tamanhos: TamanhoUniformeTrabalhador[]; carrinho: ItemCarrinhoUniforme[];
  saldos: EstoqueUniformePorObra[]; habilitado: boolean;
  aoAdicionar: (item: ItemCarrinhoUniforme) => void; aoRemover: (id: string) => void;
}) {
  return <div className="uniforme-catalogo-grade">{pecasPermitidas.map(peca => <PecaUniforme key={peca.id} peca={peca}
    grade={saldos.filter(s => s.catalogoUniformeId === peca.id)} tamanhoSugerido={tamanhos.find(t => t.catalogoUniformeId === peca.id)?.tamanho}
    item={carrinho.find(i => i.catalogoUniformeId === peca.id)} habilitado={habilitado} aoAdicionar={aoAdicionar} aoRemover={() => aoRemover(peca.id)} />)}</div>;
}
