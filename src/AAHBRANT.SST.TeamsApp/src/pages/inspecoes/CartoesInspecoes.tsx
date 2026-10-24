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
import { Add24Regular, ClipboardTaskListLtr24Regular, Delete24Regular, Home24Regular, VehicleTruckProfile24Regular } from '@fluentui/react-icons';
import { api, type TemaInspecaoResumo } from '../../lib/api';
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

interface ItemRascunho {
  chave: number;
  descricao: string;
}

export function CartoesInspecoes() {
  const [params, setParams] = useSearchParams();
  const cartao = params.get('cartao');
  const sucessoToast = useSucessoToast();
  const [temas, setTemas] = useState<TemaInspecaoResumo[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [painelAberto, setPainelAberto] = useState(false);
  const [nome, setNome] = useState('');
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
    try {
      setSalvando(true);
      setErroPainel(null);
      const { id } = await api.temasInspecao.criar(nome.trim(), descricoes.map((descricao) => ({ descricao })));
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

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: 16 }}>
        {/* Fixo: sempre o primeiro card da fila, antes de Alojamento, Veículos e dos temas. */}
        <CartaoAcionavel
          tracejado
          rotuloAria="Criar novo tema de inspeção"
          aoAbrir={painelAberto ? () => setPainelAberto(false) : abrirPainel}
        >
          <Add24Regular />
          <div style={{ fontWeight: 600 }}>{painelAberto ? 'Fechar' : 'Novo tema de inspeção'}</div>
          <Legenda>Ex.: Andaime, Escada, Extintor…</Legenda>
        </CartaoAcionavel>

        <CartaoAcionavel rotuloAria="Abrir Alojamento" aoAbrir={() => abrir('alojamento')}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontWeight: 600 }}>
            <Home24Regular /> Alojamento
          </div>
          <Legenda>Inspeção de cada alojamento, por obra.</Legenda>
        </CartaoAcionavel>

        <CartaoAcionavel rotuloAria="Abrir Veículos" aoAbrir={() => abrir('veiculos')}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontWeight: 600 }}>
            <VehicleTruckProfile24Regular /> Veículos
          </div>
          <Legenda>Retroescavadeira, escavadeira e caminhões.</Legenda>
        </CartaoAcionavel>

        {temas.map((tema) => (
          <CartaoAcionavel key={tema.id} rotuloAria={`Abrir ${tema.nome}`} aoAbrir={() => abrir(`${PREFIXO_TEMA}${tema.id}`)}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontWeight: 600 }}>
              <ClipboardTaskListLtr24Regular /> {tema.nome}
            </div>
            <div style={{ fontSize: 12 }}>
              <span style={{ fontWeight: 600 }}>{tema.totalItens}</span> {tema.totalItens === 1 ? 'item cadastrado' : 'itens cadastrados'}
            </div>
            {tema.itensComInspecaoEmAndamento > 0 && <StatusChip tom="info">{tema.itensComInspecaoEmAndamento} em andamento</StatusChip>}
          </CartaoAcionavel>
        ))}
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

      <InspecoesTab />
    </div>
  );
}
