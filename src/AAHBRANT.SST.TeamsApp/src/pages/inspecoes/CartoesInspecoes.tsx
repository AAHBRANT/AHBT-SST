import { useCallback, useEffect, useState, type KeyboardEvent, type ReactNode } from 'react';
import { useSearchParams } from 'react-router-dom';
import {
  BotaoAcao,
  BotaoVoltar,
  Button,
  Campo,
  Card,
  FeedbackInline,
  Field,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  Legenda,
  PainelCriacaoInline,
  StatusChip,
  designTokens,
  tokensUi,
} from '@ui';
import { Add24Regular, ChevronRight20Regular, ClipboardTaskListLtr20Regular, Delete24Regular, Home24Regular, VehicleTruckProfile24Regular } from '@fluentui/react-icons';
import { api, StatusInspecao, TipoInspecao, type TemaInspecaoResumo } from '../../lib/api';
import type { InspecaoCampo } from '../../lib/offline/inspecoes';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { AlojamentoTab } from './AlojamentoTab';
import { VeiculosTab } from './VeiculosTab';
import { TemaInspecaoView } from './TemaInspecaoView';
import { InspecoesTab } from './InspecoesTab';

// Valores de ?cartao= na URL: 'alojamento', 'veiculos' ou 'tema:<id>'. Sem o parâmetro, a tela mostra
// os cards e, abaixo, a lista de inspeções de sempre.
const PREFIXO_TEMA = 'tema:';

function CartaoAcionavel({ aoAbrir, rotuloAria, children, tracejado = false }: {
  aoAbrir: () => void;
  rotuloAria: string;
  children: ReactNode;
  tracejado?: boolean;
}) {
  function aoTeclar(evento: KeyboardEvent<HTMLDivElement>) {
    if (evento.key === 'Enter' || evento.key === ' ') {
      evento.preventDefault();
      aoAbrir();
    }
  }
  return (
    <Card>
      <div
        role="button"
        tabIndex={0}
        aria-label={rotuloAria}
        onClick={aoAbrir}
        onKeyDown={aoTeclar}
        style={{
          cursor: 'pointer',
          display: 'flex',
          flexDirection: 'column',
          gap: 8,
          minHeight: 92,
          justifyContent: 'center',
          ...(tracejado
            ? { border: `2px dashed ${designTokens.colorCardBorder}`, borderRadius: tokensUi.raio.md, padding: tokensUi.espaco.md, alignItems: 'center', textAlign: 'center' as const }
            : {}),
        }}
      >
        {children}
      </div>
    </Card>
  );
}

// Visual aprovado em 25/11/2026 (docs/mockups/inspecoes-visual-2026-11-25.html): Alojamento e Veículos
// em destaque, com a situação das inspeções de cada um; os temas em cartões compactos.
function CartaoDestaque({ titulo, descricao, icone, inspecoes, aoAbrir }: {
  titulo: string;
  descricao: string;
  icone: ReactNode;
  inspecoes: InspecaoCampo[];
  aoAbrir: () => void;
}) {
  const emAndamento = inspecoes.filter((i) => i.status === StatusInspecao.EmAndamento).length;
  const aguardando = inspecoes.filter((i) => i.status === StatusInspecao.AguardandoAssinatura).length;
  const comNc = inspecoes.filter((i) => i.itensNaoConformes > 0).length;
  const concluidas = inspecoes.filter((i) => i.status === StatusInspecao.Concluida).length;
  return (
    <CartaoAcionavel rotuloAria={`Abrir ${titulo}`} aoAbrir={aoAbrir}>
      <div style={{ display: 'flex', gap: 14, alignItems: 'flex-start' }}>
        <span
          aria-hidden="true"
          style={{
            flexShrink: 0, width: 44, height: 44, borderRadius: 12, display: 'grid', placeItems: 'center',
            color: 'var(--sst-acao-marca-tinta)', backgroundColor: 'var(--sst-acao-marca-wash)',
          }}
        >
          {icone}
        </span>
        <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column', gap: 2 }}>
          <div style={{ fontWeight: 800, fontSize: 15 }}>{titulo}</div>
          <Legenda>{descricao}</Legenda>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, marginTop: 8 }}>
            {emAndamento > 0 && <StatusChip tom="info">{emAndamento} em andamento</StatusChip>}
            {aguardando > 0 && <StatusChip tom="atencao">{aguardando} aguardando assinatura</StatusChip>}
            {comNc > 0 && <StatusChip tom="alerta">{comNc} com não conformidade</StatusChip>}
            {concluidas > 0 && <StatusChip tom="ok">{concluidas} {concluidas === 1 ? 'concluída' : 'concluídas'}</StatusChip>}
            {inspecoes.length === 0 && <Legenda>Nenhuma inspeção ainda.</Legenda>}
          </div>
        </div>
        <ChevronRight20Regular aria-hidden="true" style={{ alignSelf: 'center', color: designTokens.colorNeutralMedium }} />
      </div>
    </CartaoAcionavel>
  );
}

// Cartão compacto de tema. Ponto âmbar: item vencendo em até 7 dias; vermelho: item fora do prazo.
function CartaoTema({ tema, aoAbrir }: { tema: TemaInspecaoResumo; aoAbrir: () => void }) {
  const fora = tema.itensForaDoPrazo ?? 0;
  const vencendo = tema.itensVencendo ?? 0;
  const partes = [
    tema.totalItens === 0 ? 'Nenhum item cadastrado' : `${tema.totalItens} ${tema.totalItens === 1 ? 'item' : 'itens'}`,
    fora > 0 ? `${fora} fora do prazo` : null,
    vencendo > 0 ? `${vencendo} ${vencendo === 1 ? 'vence' : 'vencem'} em até 7 dias` : null,
    tema.itensComInspecaoEmAndamento > 0 ? `${tema.itensComInspecaoEmAndamento} em andamento` : null,
  ].filter(Boolean);
  const tomPonto = fora > 0 ? 'alerta' : vencendo > 0 ? 'atencao' : null;
  function aoTeclar(evento: KeyboardEvent<HTMLDivElement>) {
    if (evento.key === 'Enter' || evento.key === ' ') {
      evento.preventDefault();
      aoAbrir();
    }
  }
  return (
    <div
      role="button"
      tabIndex={0}
      aria-label={`Abrir ${tema.nome}: ${partes.join(', ')}`}
      onClick={aoAbrir}
      onKeyDown={aoTeclar}
      className="sst-cartao-tema"
      style={{
        position: 'relative', display: 'flex', alignItems: 'center', gap: 10, padding: '10px 12px', cursor: 'pointer',
        borderRadius: tokensUi.raio.md, backgroundColor: designTokens.colorSurface,
        border: `1px solid ${tokensUi.bordaSuave}`,
      }}
    >
      <span
        aria-hidden="true"
        style={{
          flexShrink: 0, width: 30, height: 30, borderRadius: 9, display: 'grid', placeItems: 'center',
          color: tema.totalItens > 0 ? tokensUi.status.ok.tinta : designTokens.colorNeutralMedium,
          backgroundColor: tema.totalItens > 0 ? tokensUi.status.ok.fundo : tokensUi.status.neutro.fundo,
        }}
      >
        <ClipboardTaskListLtr20Regular />
      </span>
      <div style={{ minWidth: 0, paddingRight: tomPonto ? 12 : 0 }}>
        <div style={{ fontSize: 13, fontWeight: tema.totalItens > 0 ? 700 : 500, lineHeight: 1.25 }}>{tema.nome}</div>
        <div style={{ fontSize: 11.5, color: designTokens.colorNeutralMedium }}>{partes.join(' · ')}</div>
      </div>
      {tomPonto && (
        <span
          aria-hidden="true"
          className={tomPonto === 'alerta' ? 'sst-ponto-pulso' : undefined}
          style={{ position: 'absolute', top: 10, right: 10, width: 8, height: 8, borderRadius: '50%', backgroundColor: tokensUi.status[tomPonto].tinta }}
        />
      )}
    </div>
  );
}

interface ItemRascunho {
  chave: number;
  descricao: string;
}

export function CartoesInspecoes() {
  const [params, setParams] = useSearchParams();
  const cartao = params.get('cartao');
  const sucessoToast = useSucessoToast();
  const [temas, setTemas] = useState<TemaInspecaoResumo[]>([]);
  const [inspecoes, setInspecoes] = useState<InspecaoCampo[]>([]);
  const [ocultarSemItens, setOcultarSemItens] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [painelAberto, setPainelAberto] = useState(false);
  const [nome, setNome] = useState('');
  const [periodicidadePadrao, setPeriodicidadePadrao] = useState('');
  const [itens, setItens] = useState<ItemRascunho[]>([{ chave: 1, descricao: '' }]);
  const [salvando, setSalvando] = useState(false);
  // Item recém-adicionado: recebe o foco para que o Enter encadeie a digitação do próximo item.
  const [chaveEmFoco, setChaveEmFoco] = useState<number | null>(null);

  const abrir = useCallback(
    (valor: string | null) => {
      setParams(
        (atual) => {
          const novo = new URLSearchParams(atual);
          if (valor) novo.set('cartao', valor);
          else novo.delete('cartao');
          return novo;
        },
        { replace: false },
      );
    },
    [setParams],
  );

  const carregarTemas = useCallback(async () => {
    try {
      setTemas(await api.temasInspecao.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar os temas de inspeção.');
    }
  }, []);

  useEffect(() => {
    void carregarTemas();
  }, [carregarTemas]);

  function abrirPainel() {
    setNome('');
    setPeriodicidadePadrao('');
    setItens([{ chave: 1, descricao: '' }]);
    setErroPainel(null);
    setPainelAberto(true);
  }

  function atualizarItem(chave: number, descricao: string) {
    setItens((atual) => atual.map((i) => (i.chave === chave ? { ...i, descricao } : i)));
  }

  function adicionarItem() {
    const chave = Math.max(0, ...itens.map((i) => i.chave)) + 1;
    setItens((atual) => [...atual, { chave, descricao: '' }]);
    setChaveEmFoco(chave);
  }

  function removerItem(chave: number) {
    setItens((atual) => (atual.length === 1 ? atual : atual.filter((i) => i.chave !== chave)));
  }

  async function criarTema() {
    const descricoes = itens.map((i) => i.descricao.trim()).filter(Boolean);
    if (!nome.trim()) return setErroPainel('Informe o nome do tema (ex.: Andaime).');
    if (descricoes.length === 0) return setErroPainel('Adicione ao menos um item ao checklist.');
    const diasPadrao = periodicidadePadrao.trim() ? Number(periodicidadePadrao) : null;
    if (diasPadrao !== null && (!Number.isInteger(diasPadrao) || diasPadrao < 1 || diasPadrao > 365))
      return setErroPainel('A periodicidade deve ficar entre 1 e 365 dias.');
    try {
      setSalvando(true);
      setErroPainel(null);
      const { id } = await api.temasInspecao.criar(nome.trim(), descricoes.map((descricao) => ({ descricao })), diasPadrao);
      sucessoToast('Tema criado com sucesso.');
      setPainelAberto(false);
      await carregarTemas();
      abrir(`${PREFIXO_TEMA}${id}`);
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Não foi possível criar o tema.');
    } finally {
      setSalvando(false);
    }
  }

  const voltar = <BotaoVoltar rotulo="Voltar aos cards de inspeção" onClick={() => abrir(null)} />;

  if (cartao === 'alojamento') {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
        <div>{voltar}</div>
        <AlojamentoTab />
      </div>
    );
  }
  if (cartao === 'veiculos') {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
        <div>{voltar}</div>
        <VeiculosTab />
      </div>
    );
  }
  if (cartao?.startsWith(PREFIXO_TEMA)) {
    const tema = temas.find((t) => t.id === cartao.slice(PREFIXO_TEMA.length));
    if (tema) return <TemaInspecaoView tema={tema} aoVoltar={() => abrir(null)} aoAlterado={() => void carregarTemas()} />;
    // Os temas ainda estão chegando (ou o link é de um tema que não existe mais).
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
        <div>{voltar}</div>
        {erro ? <FeedbackInline tom="erro">{erro}</FeedbackInline> : <Legenda>Carregando tema…</Legenda>}
      </div>
    );
  }

  const temasVisiveis = ocultarSemItens ? temas.filter((t) => t.totalItens > 0) : temas;
  const temasSemItens = temas.filter((t) => t.totalItens === 0).length;

  // Ordem aprovada em 25/11/2026: a lista de inspeções vem primeiro; os cards de Alojamento, Veículos
  // e dos temas ficam logo abaixo (antes eram 17 cards grandes empurrando a lista para baixo).
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <InspecoesTab aoCarregar={setInspecoes} />

      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: 14 }}>
        <CartaoDestaque
          titulo="Alojamento"
          descricao="Inspeção de cada alojamento, por obra."
          icone={<Home24Regular />}
          inspecoes={inspecoes.filter((i) => i.tipoInspecao === TipoInspecao.Alojamento)}
          aoAbrir={() => abrir('alojamento')}
        />
        <CartaoDestaque
          titulo="Veículos"
          descricao="Retroescavadeira, escavadeira e caminhões."
          icone={<VehicleTruckProfile24Regular />}
          inspecoes={inspecoes.filter((i) => i.tipoInspecao === TipoInspecao.Veiculo)}
          aoAbrir={() => abrir('veiculos')}
        />
      </div>

      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10, alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ fontSize: 13, fontWeight: 700, letterSpacing: '0.07em', textTransform: 'uppercase', color: designTokens.colorNeutralMedium }}>
          Temas de inspeção <span style={{ fontWeight: 500, fontVariantNumeric: 'tabular-nums' }}>· {temas.length}</span>
        </div>
        {temasSemItens > 0 && (
          <Button appearance="subtle" size="small" onClick={() => setOcultarSemItens((v) => !v)}>
            {ocultarSemItens ? `Mostrar todos os ${temas.length} temas` : `Ocultar os ${temasSemItens} temas sem itens`}
          </Button>
        )}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(210px, 1fr))', gap: 10 }}>
        {temasVisiveis.map((tema) => (
          <CartaoTema key={tema.id} tema={tema} aoAbrir={() => abrir(`${PREFIXO_TEMA}${tema.id}`)} />
        ))}
        {/* "Novo tema" vai para o fim da grade, tracejado e discreto. */}
        <div
          role="button"
          tabIndex={0}
          aria-label={painelAberto ? 'Fechar o cadastro de tema' : 'Criar novo tema de inspeção'}
          aria-expanded={painelAberto}
          onClick={painelAberto ? () => setPainelAberto(false) : abrirPainel}
          onKeyDown={(evento) => {
            if (evento.key === 'Enter' || evento.key === ' ') {
              evento.preventDefault();
              if (painelAberto) setPainelAberto(false);
              else abrirPainel();
            }
          }}
          style={{
            display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8, padding: '10px 12px', minHeight: 52,
            borderRadius: tokensUi.raio.md, border: `1.5px dashed ${designTokens.colorCardBorder}`, cursor: 'pointer',
            fontSize: 13, fontWeight: 500, color: designTokens.colorNeutralMedium,
          }}
        >
          <Add24Regular />
          {painelAberto ? 'Fechar' : 'Novo tema'}
        </div>
      </div>

      <PainelCriacaoInline aberto={painelAberto} titulo="Novo tema de inspeção">
        <FormSection titulo="Tema e checklist" numero={1} primeira>
          {erroPainel && <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>{erroPainel}</FeedbackInline>}
          <FormGrid>
            <Campo span={12}>
              <Field label="Nome do tema" required hint="É o nome do card. Depois você cadastra cada item dentro dele (ex.: cada andaime).">
                <Input value={nome} maxLength={120} placeholder="Ex.: Andaime" onChange={(_, d) => setNome(d.value)} />
              </Field>
            </Campo>
            <Campo span={12}>
              <Field label="Inspecionar a cada (dias)" hint="Opcional. Vira o padrão de cada item cadastrado neste tema (ex.: 30). Cada item pode ter o seu e o seu responsável.">
                <Input
                  type="number"
                  min={1}
                  max={365}
                  value={periodicidadePadrao}
                  placeholder="Ex.: 30"
                  onChange={(_, d) => setPeriodicidadePadrao(d.value)}
                />
              </Field>
            </Campo>
            <Campo span={12}>
              <Field label="Itens do checklist" required hint="Cada item é respondido como Conforme ou Não Conforme; a foto é obrigatória nos Não Conforme.">
                <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                  {itens.map((item, indice) => (
                    <div key={item.chave} style={{ display: 'grid', gridTemplateColumns: '1fr auto', gap: 8, alignItems: 'center' }}>
                      <Input
                        value={item.descricao}
                        maxLength={500}
                        autoFocus={item.chave === chaveEmFoco}
                        placeholder={`Item ${indice + 1} (ex.: Guarda-corpo instalado)`}
                        aria-label={`Item ${indice + 1} do checklist`}
                        onChange={(_, d) => atualizarItem(item.chave, d.value)}
                        onKeyDown={(e) => {
                          if (e.key === 'Enter' && item.descricao.trim()) {
                            e.preventDefault();
                            adicionarItem();
                          }
                        }}
                      />
                      <BotaoAcao
                        tom="excluir"
                        icon={<Delete24Regular />}
                        onClick={() => removerItem(item.chave)}
                        disabled={itens.length === 1}
                        aria-label={`Remover item ${indice + 1}`}
                      />
                    </div>
                  ))}
                  <div>
                    <Button icon={<Add24Regular />} onClick={adicionarItem}>Adicionar item</Button>
                  </div>
                </div>
              </Field>
            </Campo>
          </FormGrid>
          <FormRodape>
            <Button onClick={() => setPainelAberto(false)}>Cancelar</Button>
            <Button appearance="primary" onClick={criarTema} disabled={salvando}>Criar tema</Button>
          </FormRodape>
        </FormSection>
      </PainelCriacaoInline>
    </div>
  );
}
