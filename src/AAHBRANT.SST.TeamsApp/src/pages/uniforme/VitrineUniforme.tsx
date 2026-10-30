import { useState } from 'react';
import { Button, Field, Input, Select, StatusChip, Dialog, DialogSurface, DialogBody, DialogTitle, DialogContent, DialogActions } from '@ui';
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
  const [detalhes, setDetalhes] = useState(false);
  const saldo = grade.find(g => g.tamanho === tamanho)?.saldo ?? 0;
  const qtd = Number(quantidade);
  const funcoes = peca.funcoesAtribuidas ?? [];
  return <article className={`uniforme-peca ${item ? 'uniforme-peca-selecionada' : ''}`}>
    <FotoCatalogoUniforme catalogoUniformeId={peca.id} temFoto={peca.temFoto} referenciaVisual={peca.referenciaVisual} destaque />
    <div className="uniforme-peca-corpo">
      <strong>{peca.nome}</strong>
      {referencia && <span className="uniforme-meta">{referencia.cor} · {referencia.tecido}</span>}
      {referencia && <Button appearance="subtle" size="small" onClick={() => setDetalhes(true)}>Ver ficha da peça</Button>}
      <Field label="Tamanho para entrega">
        <Select value={tamanho} disabled={!!item} onChange={(_, d) => setEscolhido(d.value)} aria-label={`Tamanho de ${peca.nome}`}>
          {tamanhos.map(t => <option key={t} value={t}>{t}</option>)}
        </Select>
      </Field>
      <StatusChip tom={!habilitado ? 'neutro' : saldo > 0 ? 'ok' : 'alerta'}>
        {!habilitado ? 'Selecione o trabalhador para entregar' : saldo > 0 ? `${saldo} unidade(s) disponíveis` : 'Indisponível neste tamanho'}
      </StatusChip>
      <span className="uniforme-meta">Saldo por tamanho na obra selecionada</span>
      <div className="uniforme-grade" aria-label={`Grade de ${peca.nome}`}>
        {tamanhos.map(t => <span key={t} className={t === tamanho ? 'uniforme-tamanho-ativo' : ''}>{t}: <b>{grade.find(g => g.tamanho === t)?.saldo ?? 0}</b></span>)}
      </div>
      <div className="uniforme-funcoes"><span className="uniforme-meta">Funções atribuídas na matriz</span><span>{funcoes.length ? funcoes.join(' · ') : 'Nenhuma função vinculada'}</span>
        {referencia && <span className="uniforme-meta">Ficha: {referencia.grupo ?? 'Uso em chuva e atividades externas'}{referencia.funcoesIndicadas.length ? ` · ${referencia.funcoesIndicadas.join(' · ')}` : ''}</span>}
      </div>
      {item ? <><span className="uniforme-meta">No kit: {item.quantidade} de {saldo} disponíveis · {item.tamanho}</span><Button icon={<Delete24Regular />} onClick={aoRemover}>Remover do kit</Button></> :
        <div className="uniforme-adicionar"><Input type="number" min={1} max={saldo} step={1} value={quantidade} onChange={(_, d) => setQuantidade(d.value)} aria-label={`Quantidade de ${peca.nome}`} />
          <Button appearance="primary" icon={<AddCircle24Regular />} disabled={!habilitado || !Number.isInteger(qtd) || qtd < 1 || qtd > saldo} onClick={() => aoAdicionar({ catalogoUniformeId: peca.id, quantidade: qtd, tamanho })}>Adicionar</Button></div>}
    </div>
    {referencia && <Dialog open={detalhes} onOpenChange={(_, d) => setDetalhes(d.open)}><DialogSurface style={{ maxWidth: 1100 }}><DialogBody><DialogTitle>{peca.nome} · {referencia.cor}</DialogTitle><DialogContent><img src={referencia.arquivo} alt={`Ficha de ${peca.nome}`} style={{ width: '100%' }} /></DialogContent><DialogActions><Button onClick={() => setDetalhes(false)}>Fechar</Button></DialogActions></DialogBody></DialogSurface></Dialog>}
  </article>;
}

export function VitrineUniforme({ pecasPermitidas, tamanhos, carrinho, saldos, habilitado, aoAdicionar, aoRemover }: {
  pecasPermitidas: CatalogoUniforme[]; tamanhos: TamanhoUniformeTrabalhador[]; carrinho: ItemCarrinhoUniforme[];
  saldos: EstoqueUniformePorObra[]; habilitado: boolean;
  aoAdicionar: (item: ItemCarrinhoUniforme) => void; aoRemover: (id: string) => void;
}) {
  return <div className="uniforme-catalogo-grade">{pecasPermitidas.map(peca => <PecaUniforme key={peca.id} peca={peca}
    grade={saldos.filter(s => s.catalogoUniformeId === peca.id)} tamanhoSugerido={tamanhos.find(t => t.catalogoUniformeId === peca.id)?.tamanho}
    item={carrinho.find(i => i.catalogoUniformeId === peca.id)} habilitado={habilitado} aoAdicionar={aoAdicionar} aoRemover={() => aoRemover(peca.id)} />)}</div>;
}
