import { useMemo, useState } from 'react';
import { Button, FeedbackInline, Field, Input, Legenda, Textarea, designTokens } from '@ui';
import { Add24Regular, Checkmark20Filled, Dismiss24Regular, Search20Regular } from '@fluentui/react-icons';
import { api, type CatalogoTemaDds } from '../../lib/api';

const normalizar = (texto: string) => texto.normalize('NFD').replace(/[̀-ͯ]/g, '').toLocaleLowerCase('pt-BR');

interface Props {
  temas: CatalogoTemaDds[];
  /** Id do tema escolhido ('' = nenhum). */
  selecionadoId: string;
  aoSelecionar: (id: string) => void;
  /** Chamado depois que um tema novo é cadastrado aqui mesmo; o pai inclui na lista e seleciona. */
  aoCriar: (tema: CatalogoTemaDds) => void;
}

const cartao = (marcado: boolean) => ({
  display: 'grid', gap: 2, width: '100%', textAlign: 'left' as const, cursor: 'pointer',
  padding: '10px 12px', borderRadius: 6, font: 'inherit', color: 'inherit',
  border: `1px solid ${marcado ? designTokens.colorPrimary : designTokens.colorCardBorder}`,
  backgroundColor: marcado ? designTokens.colorNeutralLight : designTokens.colorSurface,
});

// Tema livre do DDS (ex.: "Outubro Rosa"): lista própria de temas criados pelos usuários, com busca e
// cadastro rápido ali mesmo. Um tema por registro; "Nenhum" limpa a escolha.
export function SeletorTemaLivre({ temas, selecionadoId, aoSelecionar, aoCriar }: Props) {
  const [busca, setBusca] = useState('');
  const [criando, setCriando] = useState(false);
  const [nome, setNome] = useState('');
  const [descricao, setDescricao] = useState('');
  const [salvando, setSalvando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  const filtrados = useMemo(() => {
    const q = normalizar(busca.trim());
    return q ? temas.filter((t) => normalizar(`${t.nome} ${t.descricao ?? ''}`).includes(q)) : temas;
  }, [busca, temas]);

  async function salvar() {
    if (!nome.trim()) { setErro('Informe o nome do tema.'); return; }
    setSalvando(true);
    setErro(null);
    try {
      const { id } = await api.catalogoTemasDds.criar(nome.trim(), descricao.trim() || null);
      aoCriar({ id, nome: nome.trim(), descricao: descricao.trim() || null });
      setNome('');
      setDescricao('');
      setBusca('');
      setCriando(false);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível cadastrar o tema.');
    } finally {
      setSalvando(false);
    }
  }

  return <div style={{ display: 'grid', gap: 10 }}>
    <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
      <div style={{ flex: '1 1 220px', minWidth: 0 }}>
        <Input value={busca} onChange={(_, d) => setBusca(d.value)} contentBefore={<Search20Regular />}
          placeholder="Buscar tema" aria-label="Buscar tema livre" />
      </div>
      <Button icon={criando ? <Dismiss24Regular /> : <Add24Regular />} onClick={() => { setCriando(!criando); setErro(null); }}>
        {criando ? 'Cancelar' : 'Novo tema'}
      </Button>
    </div>

    {criando && <div style={{ display: 'grid', gap: 10, padding: 12, borderRadius: 6, border: `1px dashed ${designTokens.colorCardBorder}` }}>
      <Field label="Nome do tema" required>
        <Input value={nome} maxLength={200} onChange={(_, d) => setNome(d.value)} placeholder="Ex.: Outubro Rosa" />
      </Field>
      <Field label="Descrição (opcional)">
        <Textarea value={descricao} maxLength={1000} rows={3} onChange={(_, d) => setDescricao(d.value)}
          placeholder="O que será abordado neste tema" />
      </Field>
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
      <div><Button appearance="primary" disabled={salvando} onClick={() => void salvar()}>
        {salvando ? 'Salvando…' : 'Cadastrar e usar'}
      </Button></div>
    </div>}

    <div role="radiogroup" aria-label="Tema livre" style={{ display: 'grid', gap: 6, maxHeight: 280, overflowY: 'auto' }}>
      <button type="button" role="radio" aria-checked={selecionadoId === ''} style={cartao(selecionadoId === '')}
        onClick={() => aoSelecionar('')}>
        <strong>Nenhum tema livre</strong>
        <Legenda>O DDS usa apenas as atividades escolhidas.</Legenda>
      </button>
      {filtrados.map((t) => {
        const marcado = t.id === selecionadoId;
        return <button key={t.id} type="button" role="radio" aria-checked={marcado} style={cartao(marcado)}
          onClick={() => aoSelecionar(marcado ? '' : t.id)}>
          <span style={{ display: 'flex', gap: 8, alignItems: 'center', justifyContent: 'space-between' }}>
            <strong>{t.nome}</strong>
            {marcado && <Checkmark20Filled aria-hidden="true" />}
          </span>
          {t.descricao && <span style={{
            display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden',
            fontSize: 12, color: designTokens.colorNeutralMedium,
          }}>{t.descricao}</span>}
        </button>;
      })}
      {filtrados.length === 0 && <Legenda>
        {temas.length === 0 ? 'Nenhum tema cadastrado ainda. Use "Novo tema" para criar o primeiro.' : `Nenhum tema encontrado para “${busca}”.`}
      </Legenda>}
    </div>
  </div>;
}
