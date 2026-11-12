import { useEffect, useRef, useState } from 'react';
import {
  Button,
  Dialog, DialogBody, DialogContent, DialogSurface, DialogTitle,
  FeedbackInline,
  Field,
  Input,
  Textarea,
} from '@ui';
import { Add24Regular, Dismiss24Regular } from '@fluentui/react-icons';
import { mergeClasses, useLancamentoStyles } from './estilosUniforme';
import { FotoCatalogoUniforme } from './FotoCatalogoUniforme';
import { referenciaUniforme } from './referenciasUniforme';
import type { CatalogoUniforme } from '../../lib/api';

// Lançamento de estoque (entrada ou ajuste de saldo) em diálogo curto — no celular vira folha na base
// da tela. Mockup aprovado: docs/mockups/2026-11-11-uniforme-melhorias-v1.html (Seção 2, opção A).
// O diálogo não chama a API: devolve os dados para a tela, que grava e recarrega. A confirmação da obra
// e a pergunta de descarte são etapas DENTRO deste mesmo diálogo (sem diálogo empilhado), assim o foco,
// o Esc e o Tab sempre pertencem a uma única superfície.

export const QUANTIDADE_MAXIMA = 99999;
const TAMANHO_MAX_CARACTERES = 20;
const OBSERVACAO_MAX_CARACTERES = 300;

export interface TamanhoComSaldo {
  tamanho: string;
  saldo: number;
}

export interface DadosLancamento {
  tamanho: string;
  quantidade: number;
  observacao: string;
}

interface Props {
  modo: 'entrada' | 'ajuste';
  item: CatalogoUniforme;
  nomeObra: string;
  tamanhos: TamanhoComSaldo[];
  tamanhoInicial: string;
  // Devolve a mensagem de erro (mostrada no próprio diálogo) ou null quando deu certo.
  aoConfirmar: (dados: DadosLancamento) => Promise<string | null>;
  aoFechar: () => void;
}

type Etapa = 'formulario' | 'confirmar' | 'descartar';

export function LancamentoEstoqueDialog({ modo, item, nomeObra, tamanhos, tamanhoInicial, aoConfirmar, aoFechar }: Props) {
  const e = useLancamentoStyles();
  const entrada = modo === 'entrada';
  const referencia = referenciaUniforme(item.referenciaVisual);
  const saldoInicial = tamanhos.find((t) => t.tamanho === tamanhoInicial)?.saldo ?? 0;
  const quantidadeInicial = entrada ? '1' : String(saldoInicial);
  // Sem tamanho existente para escolher, o único caminho é digitar um novo.
  const semTamanhos = entrada && tamanhos.length === 0;

  const [etapa, setEtapa] = useState<Etapa>('formulario');
  const [tamanho, setTamanho] = useState(tamanhoInicial);
  const [novoTamanho, setNovoTamanho] = useState(semTamanhos && !tamanhoInicial);
  const [textoTamanho, setTextoTamanho] = useState('');
  const [quantidade, setQuantidade] = useState(quantidadeInicial);
  const [observacao, setObservacao] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  const primeiroChipRef = useRef<HTMLButtonElement | null>(null);
  const novoTamanhoRef = useRef<HTMLInputElement | null>(null);
  const quantidadeRef = useRef<HTMLInputElement | null>(null);
  const botaoEtapaRef = useRef<HTMLButtonElement | null>(null);

  // Normalizado como a API faz (Trim + maiúsculas): é o que aparece na confirmação e é enviado.
  const tamanhoEscolhido = (novoTamanho ? textoTamanho : tamanho).trim().toUpperCase();
  const numero = quantidade.trim() === '' ? NaN : Number(quantidade);
  const acimaDoLimite = Number.isFinite(numero) && numero > QUANTIDADE_MAXIMA;
  const quantidadeValida = Number.isInteger(numero) && (entrada ? numero > 0 : numero >= 0) && !acimaDoLimite;
  const podeEnviar = !enviando && tamanhoEscolhido !== '' && quantidadeValida && (entrada || observacao.trim() !== '');
  const alterado =
    observacao.trim() !== '' ||
    textoTamanho.trim() !== '' ||
    quantidade !== quantidadeInicial ||
    (!novoTamanho && tamanho !== tamanhoInicial);

  // Foco inicial: o tamanho quando não vem pré-selecionado; senão, a quantidade. A cada troca de etapa o
  // foco vai para o botão principal da etapa (ou volta ao formulário).
  useEffect(() => {
    const t = window.setTimeout(() => {
      if (etapa !== 'formulario') {
        botaoEtapaRef.current?.focus();
      } else if (entrada && !tamanhoInicial) {
        (novoTamanho ? novoTamanhoRef.current : primeiroChipRef.current)?.focus();
      } else {
        quantidadeRef.current?.focus();
        quantidadeRef.current?.select();
      }
    }, 60);
    return () => window.clearTimeout(t);
    // Só no primeiro render e quando a etapa muda.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [etapa]);

  // Escolheu "Novo tamanho": o cursor vai direto para o campo de texto.
  useEffect(() => {
    if (novoTamanho && etapa === 'formulario') novoTamanhoRef.current?.focus();
  }, [novoTamanho, etapa]);

  function ajustarQuantidade(delta: number) {
    const atual = Number.isFinite(Number(quantidade)) ? Math.trunc(Number(quantidade)) : 0;
    setQuantidade(String(Math.min(QUANTIDADE_MAXIMA, Math.max(0, atual + delta))));
  }

  // Esc, X, clique fora e Cancelar passam por aqui.
  function pedirFechar() {
    if (enviando) return;
    if (etapa === 'confirmar' || etapa === 'descartar') {
      setEtapa('formulario');
      return;
    }
    if (alterado) setEtapa('descartar');
    else aoFechar();
  }

  async function enviar() {
    if (!podeEnviar) return;
    setEnviando(true);
    setErro(null);
    const mensagem = await aoConfirmar({ tamanho: tamanhoEscolhido, quantidade: numero, observacao: observacao.trim() });
    setEnviando(false);
    if (mensagem) {
      setErro(mensagem);
      setEtapa('formulario');
    }
  }

  const verbo = entrada ? 'Registrar entrada' : 'Ajustar saldo';
  const mensagemConfirmacao = entrada
    ? `Registrar entrada de ${numero} un. de ${item.nome} (tam. ${tamanhoEscolhido}) na obra "${nomeObra}"?`
    : `Ajustar o saldo de ${item.nome} (tam. ${tamanhoEscolhido}) na obra "${nomeObra}" para ${numero} un.?`;

  return (
    <Dialog open onOpenChange={(_, d) => { if (!d.open) pedirFechar(); }}>
      <DialogSurface className={e.superficie}>
        <DialogBody style={{ display: 'block' }}>
          <div className={e.cabecalho}>
            <FotoCatalogoUniforme catalogoUniformeId={item.id} temFoto={item.temFoto} referenciaVisual={item.referenciaVisual} tamanho={56} />
            <div style={{ minWidth: 0 }}>
              <DialogTitle as="h3" className={e.titulo}>{verbo} · <span className={e.pecaNome}>{item.nome}</span></DialogTitle>
              <p className={e.sub}>{item.categoria || 'Sem categoria'} · {referencia?.cor ?? 'Foto própria'}</p>
            </div>
            <Button className={e.fechar} appearance="subtle" icon={<Dismiss24Regular />} onClick={pedirFechar} aria-label="Fechar" disabled={enviando} />
          </div>

          <DialogContent style={{ padding: 0, overflow: 'visible' }}>
            {etapa === 'confirmar' && (
              <>
                <div className={e.corpoEtapa} role="group" aria-live="polite">
                  <b className={e.tituloEtapa}>Confirme a obra do lançamento</b>
                  {mensagemConfirmacao}
                </div>
                <div className={e.rodape}>
                  <Button className={e.botao} onClick={() => setEtapa('formulario')} disabled={enviando}>Voltar</Button>
                  <Button ref={botaoEtapaRef} className={mergeClasses(e.botao, e.botaoPrimario)} appearance="primary" onClick={enviar} disabled={enviando}>
                    Confirmar e salvar
                  </Button>
                </div>
              </>
            )}

            {etapa === 'descartar' && (
              <>
                <div className={e.corpoEtapa} role="group" aria-live="polite">
                  <b className={e.tituloEtapa}>Descartar as alterações?</b>
                  O que você preencheu ainda não foi salvo.
                </div>
                <div className={e.rodape}>
                  <Button ref={botaoEtapaRef} className={e.botao} onClick={() => setEtapa('formulario')}>Continuar editando</Button>
                  <Button className={mergeClasses(e.botao, e.botaoDestrutivo)} appearance="primary" onClick={aoFechar}>Descartar</Button>
                </div>
              </>
            )}

            {etapa === 'formulario' && (
              <>
                {erro && (
                  <div style={{ marginTop: 12 }}>
                    <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>
                  </div>
                )}

                {entrada ? (
                  <>
                    <span className={e.rotulo} id="lanc-tamanho">Tamanho</span>
                    <div className={e.chips} role="group" aria-labelledby="lanc-tamanho">
                      {tamanhos.map((t, i) => {
                        const ativo = !novoTamanho && tamanho === t.tamanho;
                        return (
                          <Button
                            key={t.tamanho}
                            ref={i === 0 ? primeiroChipRef : undefined}
                            appearance="outline"
                            aria-pressed={ativo}
                            className={mergeClasses(e.chip, ativo && e.chipAtivo)}
                            onClick={() => { setNovoTamanho(false); setTamanho(t.tamanho); }}
                          >
                            <b style={{ display: 'block', fontSize: 15 }}>{t.tamanho}</b>
                            <small className={e.chipSaldo}>{t.saldo} un.</small>
                          </Button>
                        );
                      })}
                      <Button
                        appearance="outline"
                        icon={<Add24Regular fontSize={18} />}
                        aria-pressed={novoTamanho}
                        className={mergeClasses(e.chip, e.chipNovo, novoTamanho && e.chipAtivo)}
                        onClick={() => { setNovoTamanho(true); setTamanho(''); }}
                      >
                        Novo tamanho
                      </Button>
                    </div>
                    {novoTamanho && (
                      <Input
                        ref={novoTamanhoRef}
                        className={mergeClasses(e.entrada, e.novoTamanho)}
                        value={textoTamanho}
                        maxLength={TAMANHO_MAX_CARACTERES}
                        onChange={(_, d) => setTextoTamanho(d.value)}
                        placeholder="Digite o tamanho (ex.: M, 42...)"
                        aria-label="Novo tamanho"
                      />
                    )}
                  </>
                ) : (
                  <div className={e.selInfo}>
                    Tamanho <b className={e.selInfoDestaque}>{tamanhoInicial}</b> · saldo atual <b>{saldoInicial}</b> un.
                  </div>
                )}

                <Field
                  className={e.campo}
                  label={entrada ? 'Quantidade' : 'Novo saldo'}
                  validationState={acimaDoLimite ? 'error' : 'none'}
                  validationMessage={acimaDoLimite ? `O máximo por lançamento é ${QUANTIDADE_MAXIMA.toLocaleString('pt-BR')} un.` : undefined}
                >
                  <div className={e.qtd}>
                    <Button appearance="subtle" className={e.qtdBotao} aria-label={entrada ? 'Diminuir quantidade' : 'Diminuir saldo'} onClick={() => ajustarQuantidade(-1)}>&minus;</Button>
                    <Input
                      ref={quantidadeRef}
                      className={e.qtdInput}
                      input={{ style: { textAlign: 'center', fontWeight: 700, fontSize: 18 } }}
                      value={quantidade}
                      maxLength={7}
                      inputMode="numeric"
                      onChange={(_, d) => setQuantidade(d.value.replace(/\D/g, ''))}
                    />
                    <Button appearance="subtle" className={e.qtdBotao} aria-label={entrada ? 'Aumentar quantidade' : 'Aumentar saldo'} onClick={() => ajustarQuantidade(1)}>+</Button>
                  </div>
                </Field>

                <Field
                  className={e.campo}
                  label={<>Observação <span className={e.rotuloNota}>({entrada ? 'opcional' : 'obrigatória'})</span></>}
                >
                  {entrada ? (
                    <Input className={e.entrada} value={observacao} maxLength={OBSERVACAO_MAX_CARACTERES} onChange={(_, d) => setObservacao(d.value)} placeholder="ex.: NF 48215" />
                  ) : (
                    <Textarea
                      value={observacao}
                      maxLength={OBSERVACAO_MAX_CARACTERES}
                      onChange={(_, d) => setObservacao(d.value)}
                      placeholder="ex.: contagem física do inventário"
                      resize="vertical"
                      textarea={{ style: { minHeight: 84 } }}
                    />
                  )}
                </Field>
                {!entrada && <div className={e.nota}>O ajuste fica registrado no Histórico do tamanho.</div>}

                <div className={e.rodape}>
                  <Button className={e.botao} onClick={pedirFechar} disabled={enviando}>Cancelar</Button>
                  <Button className={mergeClasses(e.botao, e.botaoPrimario)} appearance="primary" onClick={() => setEtapa('confirmar')} disabled={!podeEnviar}>
                    {verbo}
                  </Button>
                </div>
              </>
            )}
          </DialogContent>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
