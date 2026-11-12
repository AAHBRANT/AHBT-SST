import { useEffect, useState } from 'react';
import {
  BotaoAcao,
  Button,
  Campo,
  Card,
  Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  Select,
  Legenda,
  PageHeader,
  PainelCriacaoInline,
  designTokens,
  useConfirmar,
} from '@ui';
import { Add24Regular, Delete24Regular, Save24Regular, ZoomIn24Regular } from '@fluentui/react-icons';
import { api, type CatalogoUniforme, type NovoCatalogoUniforme } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { SeletorFotoCamera } from '../../components/SeletorFotoCamera';
import { SlotFotoRemota } from '../../components/camera/SlotFotoRemota';

import { referenciasUniforme, referenciaUniforme } from './referenciasUniforme';
import { FotoCatalogoUniforme } from './FotoCatalogoUniforme';

const itemVazio: NovoCatalogoUniforme = { nome: '', categoria: '' };

// Catálogo de Uniforme (peça em si, sem tamanho embutido — o tamanho é uma dimensão do estoque e
// do cadastro do trabalhador, ver EstoqueUniformeTab.tsx e pages/pessoas/TamanhosUniformeSecao.tsx).
// Mesmo padrão de CatalogoTab.tsx (EPI), incluindo foto do item (decisão do usuário, 2026-09-07).
// Camada ui/: o formulário de cadastro empurrava a lista pra baixo (padrão do Guia §2) — sai para um
// PainelCriacaoInline, que cresce acima da lista em vez de cobrir a tela com um drawer (migração de
// 2026-09-11, mesmo padrão de AtividadesTab.tsx/InspecoesTab.tsx). Erro do painel isolado do erro da
// lista. Edição inline por linha preservada (clicar na linha edita nela mesma).
export function CatalogoUniformeTab() {
  const [itens, setItens] = useState<CatalogoUniforme[]>([]);
  const [novoItem, setNovoItem] = useState<NovoCatalogoUniforme>(itemVazio);
  const [fotoNovoItem, setFotoNovoItem] = useState<File | null>(null);
  const [edicaoId, setEdicaoId] = useState<string | null>(null);
  const [edicao, setEdicao] = useState<CatalogoUniforme | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  // Erro do painel de criação fica separado do erro da lista — mesmo padrão de
  // AtividadesTab.tsx/InspecoesTab.tsx: erro de carga da lista e erro de criação são estados
  // independentes, não faz sentido um fechar/limpar o outro.
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const [fotoAmpliada, setFotoAmpliada] = useState<{ item: CatalogoUniforme; url: string; revogar: boolean } | null>(null);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      setItens(await api.catalogosUniforme.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar catálogo de uniforme.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  // Todo caminho de fechar o painel limpa o erro e o rascunho — senão reabrir mostra dado/mensagem velha.
  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
    setNovoItem(itemVazio);
    setFotoNovoItem(null);
  }

  async function criar() {
    try {
      setCarregando(true);
      setErroPainel(null);
      const { id } = await api.catalogosUniforme.criar(novoItem);
      if (fotoNovoItem) {
        await api.catalogosUniforme.anexarFoto(id, fotoNovoItem);
      }
      await carregar();
      sucessoToast('Peça de uniforme cadastrada com sucesso.');
      fecharPainel();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar peça de uniforme.');
    } finally {
      setCarregando(false);
    }
  }

  async function trocarFoto(itemId: string, arquivo: File) {
    try {
      setErro(null);
      await api.catalogosUniforme.anexarFoto(itemId, arquivo);
      await carregar();
      sucessoToast('Foto da peça atualizada.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao enviar a foto da peça.');
    }
  }

  function iniciarEdicao(item: CatalogoUniforme) {
    setEdicaoId(item.id);
    setEdicao({ ...item });
  }

  async function salvarEdicao() {
    if (!edicao) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.catalogosUniforme.atualizar(edicao);
      setEdicaoId(null);
      setEdicao(null);
      await carregar();
      sucessoToast('Peça de uniforme atualizada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao atualizar peça de uniforme.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir esta peça do catálogo de uniforme? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.catalogosUniforme.excluir(id);
      await carregar();
      sucessoToast('Peça de uniforme excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir peça de uniforme.');
    }
  }

  function modeloSelect(valor: string | null | undefined, aoMudar: (chave: string) => void) {
    return <Select value={valor ?? ''} onChange={(_, d) => aoMudar(d.value)} aria-label="Modelo visual do uniforme">
      <option value="">Foto própria / sem modelo visual</option>
      {referenciasUniforme.map(r => <option key={r.chave} value={r.chave}>{r.nome} · {r.cor}</option>)}
    </Select>;
  }

  // Peça com modelo visual AAHBRANT amplia a foto de referência; peça com foto própria baixa o original.
  async function ampliar(item: CatalogoUniforme) {
    const referencia = referenciaUniforme(item.referenciaVisual);
    if (referencia) { setFotoAmpliada({ item, url: referencia.frente, revogar: false }); return; }
    if (!item.temFoto) return;
    try {
      const blob = await api.catalogosUniforme.baixarFoto(item.id);
      setFotoAmpliada({ item, url: URL.createObjectURL(blob), revogar: true });
    } catch (e) { setErro(e instanceof Error ? e.message : 'Falha ao carregar a foto original.'); }
  }

  function fecharFotoAmpliada() {
    if (fotoAmpliada?.revogar) URL.revokeObjectURL(fotoAmpliada.url);
    setFotoAmpliada(null);
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <PageHeader
        titulo="Catálogo de Uniforme"
        acoes={
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painelAberto ? fecharPainel() : setPainelAberto(true))}
            aria-expanded={painelAberto}
            aria-controls="painel-nova-peca-uniforme"
          >
            {painelAberto ? 'Fechar' : 'Nova peça'}
          </Button>
        }
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <div id="painel-nova-peca-uniforme">
        <PainelCriacaoInline aberto={painelAberto} titulo="Nova peça de uniforme">
          <FormSection titulo="Informações da peça" numero={1} primeira>
            {erroPainel && (
              <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
                {erroPainel}
              </FeedbackInline>
            )}
            <FormGrid>
              <Campo span={12}><Field label="Modelo visual AAHBRANT">{modeloSelect(novoItem.referenciaVisual, chave => {
                const ref = referenciaUniforme(chave); setNovoItem({ ...novoItem, referenciaVisual: chave || null, ...(ref ? { nome: ref.nome, categoria: ref.categoria } : {}) }); setFotoNovoItem(null);
              })}</Field></Campo>
              <Campo span={6}>
                <Field label="Nome (ex.: Camisa, Calça, Bota)">
                  <Input value={novoItem.nome} onChange={(_, d) => setNovoItem({ ...novoItem, nome: d.value.toUpperCase() })} />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Categoria (opcional)">
                  <Input
                    value={novoItem.categoria ?? ''}
                    onChange={(_, d) => setNovoItem({ ...novoItem, categoria: d.value })}
                  />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Foto da peça">
                  <SeletorFotoCamera
                    rotulo={fotoNovoItem ? fotoNovoItem.name : 'Tirar foto ou escolher arquivo'}
                    tiposAceitos="image/jpeg,image/png"
                    aoSelecionarArquivo={(arquivo) => setFotoNovoItem(arquivo)}
                    aoErroValidacao={setErroPainel}
                  />
                </Field>
              </Campo>
            </FormGrid>
            <FormRodape>
              <Button onClick={fecharPainel}>Cancelar</Button>
              <Button
                appearance="primary"
                icon={<Add24Regular />}
                onClick={criar}
                disabled={carregando || !novoItem.nome.trim()}
              >
                Adicionar peça
              </Button>
            </FormRodape>
          </FormSection>
        </PainelCriacaoInline>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(250px, 1fr))', gap: 16 }}>
        {itens.map((item) => {
          const editando = edicaoId === item.id && edicao ? edicao : null;
          const referenciaVisual = editando ? editando.referenciaVisual : item.referenciaVisual;
          const referencia = referenciaUniforme(referenciaVisual);
          return (
            <Card key={item.id} densidade="compacta">
              <div style={{ fontWeight: 800, fontSize: 16, minHeight: 42 }}>
                {editando ? (
                  <Input value={editando.nome} onChange={(_, d) => setEdicao({ ...editando, nome: d.value.toUpperCase() })} aria-label="Nome da peça" />
                ) : (
                  item.nome
                )}
              </div>
              <div style={{ color: designTokens.colorNeutralMedium, fontSize: 12, marginBottom: 10 }}>
                {editando ? (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                    <Input value={editando.categoria ?? ''} onChange={(_, d) => setEdicao({ ...editando, categoria: d.value })} aria-label="Categoria" placeholder="Categoria" />
                    {modeloSelect(editando.referenciaVisual, (chave) => setEdicao({ ...editando, referenciaVisual: chave || null }))}
                  </div>
                ) : (
                  <>{item.categoria || 'Sem categoria'} · {referencia?.cor ?? 'Foto própria'}</>
                )}
              </div>
              <div style={{ minHeight: 220, background: designTokens.colorNeutralLight, borderRadius: 8, display: 'grid', placeItems: 'center', position: 'relative', overflow: 'hidden' }}>
                {editando && !editando.referenciaVisual ? (
                  <SlotFotoRemota
                    key={`${item.id}-propria`}
                    rotulo="Foto da peça"
                    id={item.id}
                    temFoto={item.temFoto}
                    baixarFoto={api.catalogosUniforme.baixarFoto}
                    tamanho="compacta"
                    aoSelecionarArquivo={(arquivo) => trocarFoto(item.id, arquivo)}
                    aoErroValidacao={setErro}
                  />
                ) : (
                  <FotoCatalogoUniforme catalogoUniformeId={item.id} temFoto={item.temFoto} referenciaVisual={referenciaVisual} destaque />
                )}
                {!editando && (item.temFoto || referencia) && (
                  <Button icon={<ZoomIn24Regular />} appearance="primary" onClick={() => ampliar(item)} style={{ position: 'absolute', right: 10, bottom: 10 }}>
                    Ampliar
                  </Button>
                )}
              </div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 12 }}>
                {editando ? (
                  <Button icon={<Save24Regular />} appearance="primary" onClick={salvarEdicao} disabled={carregando}>Salvar</Button>
                ) : (
                  <Button onClick={() => iniciarEdicao(item)}>Editar</Button>
                )}
                <BotaoAcao tom="excluir" icon={<Delete24Regular />} onClick={() => excluir(item.id)} aria-label="Excluir" />
              </div>
            </Card>
          );
        })}
      </div>
      {!carregandoLista && itens.length === 0 && (
        <Card>
          <Legenda>Nenhuma peça cadastrada no catálogo de uniforme ainda.</Legenda>
        </Card>
      )}
      <Legenda>O estoque (por obra e tamanho) é controlado na aba Estoque.</Legenda>

      {dialogElement}
      <Dialog open={Boolean(fotoAmpliada)} onOpenChange={(_, d) => { if (!d.open) fecharFotoAmpliada(); }}>
        <DialogSurface style={{ maxWidth: 900, width: '96vw' }}>
          <DialogBody>
            <DialogTitle>{fotoAmpliada?.item.nome}</DialogTitle>
            <DialogContent>
              {fotoAmpliada && (
                <img src={fotoAmpliada.url} alt={`${fotoAmpliada.item.nome} — imagem ampliada`} style={{ width: '100%', maxHeight: '75vh', objectFit: 'contain' }} />
              )}
            </DialogContent>
            <DialogActions>
              <Button onClick={fecharFotoAmpliada}>Fechar</Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </div>
  );
}
