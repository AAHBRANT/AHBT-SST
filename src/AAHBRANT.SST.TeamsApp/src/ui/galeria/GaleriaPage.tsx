import { useState } from 'react';
import { makeStyles } from '@fluentui/react-components';
import { useTipografia } from '../tokens/tipografia';
import { Card, PageHeader, Button, Input, StatusChip, nivelVencimento, tomDeVencimento, rotuloDeVencimento, EstadoVazio, Carregando, FeedbackInline, Abas, useAbaNaUrl, DataTable, FormSection, FormGrid, Campo, FormRodape, ChipCheckboxGroup, Field, Textarea, CampoData } from '../index';
import { Secao } from './Secao';

// Calculado uma vez no carregamento do módulo (não a cada render) para não disparar o alerta de
// pureza do oxlint sobre chamar Date.now() durante a renderização.
const validadeExemploVenceEmBreve = new Date(Date.now() + 8 * 86_400_000).toISOString().slice(0, 10);
// Massa de dados para exercitar densidade compacta + cabeçalho fixo + alinhamento de coluna.
const LINHAS_COMPACTAS = Array.from({ length: 25 }, (_, i) => ({ id: String(i + 1), funcao: `Função ${i + 1}`, cbo: `7${String(i).padStart(3, '0')}-05`, epis: (i * 7) % 13 }));

const useGaleriaStyles = makeStyles({
  largura280: { width: '280px' },
  largura360: { width: '360px' },
  larguraTotal: { width: '100%' },
  margemTopo: { marginTop: '16px' },
  coluna: { display: 'flex', flexDirection: 'column', gap: '10px' },
  titulo: { marginBottom: '24px' },
});

// Galeria viva da camada ui/ (spec §6): cada peça em todos os estados, nos dois temas (use o botão
// da topbar). Só existe em desenvolvimento — ver a rota condicional em App.tsx. É a documentação.
export function GaleriaPage() {
  const tipo = useTipografia();
  const g = useGaleriaStyles();
  const [abaPilar, setAbaPilar] = useState<'a' | 'b' | 'c'>('a');
  const [abaInterna, setAbaInterna] = useState<'p' | 'q'>('p');
  const [abaDemo, setAbaDemo] = useAbaNaUrl('demo', ['x', 'y', 'z'] as const, 'x');
  const [abertaDemo, setAbertaDemo] = useState<string | null>(null);
  const [chipsDemo, setChipsDemo] = useState<string[]>(['a']);
  return (
    <div>
      <h1 className={`${tipo.titulo} ${g.titulo}`}>Galeria da camada ui/</h1>
      <Secao titulo="Tipografia">
        <div className={`${g.coluna} ${g.larguraTotal}`}>
          <span className={tipo.display}>1.248</span>
          <span className={tipo.titulo}>Entregas de EPI</span>
          <span className={tipo.subtitulo}>Plano de ação</span>
          <span className={tipo.corpo}>Andaime sem guarda-corpo no pavimento 3.</span>
          <span className={tipo.legenda}>Pedreiro, Ponte Rio Cuiá</span>
          <span className={tipo.micro}>Vence em breve</span>
        </div>
      </Secao>
      <Secao titulo="StatusChip">
        <StatusChip tom="ok">Vigente</StatusChip>
        <StatusChip tom="atencao">Vence em 8 dias</StatusChip>
        <StatusChip tom="alerta">Vencido</StatusChip>
        <StatusChip tom="info">Aguardando assinatura</StatusChip>
        <StatusChip tom="neutro">Prevista</StatusChip>
        {(() => { const n = nivelVencimento('2099-01-01')!; return <StatusChip tom={tomDeVencimento(n)}>{rotuloDeVencimento(n)} (helper)</StatusChip>; })()}
        <StatusChip tom="alerta" pulsar="rapido">Vencido (pulsa)</StatusChip>
        <StatusChip tom="atencao" pulsar="leve">Vence em 8 dias (pulsa leve)</StatusChip>
      </Secao>
      <Secao titulo="Card">
        <Card className={g.largura280} titulo="Confortável" subtitulo="Padding xl">Conteúdo</Card>
        <Card densidade="compacta" titulo="Compacto" acoes={<Button size="small">Ação</Button>}>Conteúdo</Card>
      </Secao>
      <Secao titulo="PageHeader">
        <div className={g.larguraTotal}>
          <PageHeader titulo="Entregas de EPI" subtitulo="287 entregas ativas em 7 obras." status={<StatusChip tom="info">Em tratamento</StatusChip>} filtros={<Input placeholder="Buscar" />} acoes={<Button appearance="primary">Nova entrega</Button>} voltarPara="/ui-galeria" rotuloVoltar="Não conformidades" />
        </div>
      </Secao>
      <Secao titulo="EstadoVazio">
        <Card className={g.largura360}><EstadoVazio titulo="Nenhuma entrega registrada" descricao="Registre a primeira entrega para começar o controle de EPI desta obra." acao={{ rotulo: 'Registrar entrega', aoClicar: () => {} }} /></Card>
        <Card className={g.largura360}><EstadoVazio variante="sem-resultado" titulo="Nenhum resultado" descricao="Tente outro termo." acao={{ rotulo: 'Limpar busca', aoClicar: () => {} }} /></Card>
        <Card className={g.largura360}><EstadoVazio variante="em-construcao" titulo="Documentos e Procedimentos" descricao="Módulo reservado no menu, ainda não construído." /></Card>
      </Secao>
      <Secao titulo="Carregando">
        <Card className={g.largura360}><Carregando variante="lista" /></Card>
        <div className={g.larguraTotal}><Carregando variante="kpi" /></div>
      </Secao>
      <Secao titulo="FeedbackInline">
        <div className={g.larguraTotal}>
          <FeedbackInline tom="erro" aoFechar={() => {}}>Não foi possível salvar. Defina o responsável antes de enviar.</FeedbackInline>
          <FeedbackInline tom="info" acao={{ rotulo: 'Ver treinamento', aoClicar: () => {} }}>Campos de NR-06 preenchidos a partir do último treinamento.</FeedbackInline>
          <FeedbackInline tom="sucesso">Entrega registrada.</FeedbackInline>
        </div>
      </Secao>
      <Secao titulo="Abas (a de módulo sincroniza com ?demo= na URL — troque e aperte F5)">
        <div className={g.larguraTotal}>
          <Abas nivel="pilar" abas={[{ valor: 'a', rotulo: 'PGR e GRO' }, { valor: 'b', rotulo: 'PCMSO' }, { valor: 'c', rotulo: 'Treinamentos', contador: 14 }]} valor={abaPilar} aoMudar={setAbaPilar} />
          <Abas nivel="modulo" abas={[{ valor: 'x', rotulo: 'Entregas' }, { valor: 'y', rotulo: 'Estoque' }, { valor: 'z', rotulo: 'Catálogo' }]} valor={abaDemo} aoMudar={setAbaDemo} />
          <Abas nivel="interno" abas={[{ valor: 'p', rotulo: 'Por obra' }, { valor: 'q', rotulo: 'Por função' }]} valor={abaInterna} aoMudar={setAbaInterna} />
        </div>
      </Secao>
      <Secao titulo="DataTable">
        <div className={g.larguraTotal}>
          <Card densidade="compacta">
            <DataTable
              aria-label="Entregas de exemplo"
              colunas={[
                { chave: 'nome', rotulo: 'Funcionário' },
                { chave: 'epi', rotulo: 'EPI' },
                { chave: 'validade', rotulo: 'Validade', render: (l) => { const n = nivelVencimento(l.validade); return n ? <StatusChip tom={tomDeVencimento(n)}>{rotuloDeVencimento(n)}</StatusChip> : '—'; } },
              ]}
              linhas={[
                { id: '1', nome: 'João da Silva', epi: 'Capacete classe B', validade: '2099-03-12' },
                { id: '2', nome: 'Ana Carolina Reis', epi: 'Luva isolante', validade: validadeExemploVenceEmBreve },
                { id: '3', nome: 'Roberto Pereira', epi: 'Cinto paraquedista', validade: '2020-02-02' },
              ]}
              chaveLinha={(l) => l.id}
              acoesLinha={() => <Button size="small" appearance="subtle">Assinar</Button>}
              expansivel={{ aberta: (l) => l.id === abertaDemo, render: (l) => <span>Detalhe de {l.nome}</span> }}
              aoClicarLinha={(l) => setAbertaDemo((a) => (a === l.id ? null : l.id))}
            />
          </Card>
          <Card densidade="compacta" className={g.margemTopo}><DataTable colunas={[{ chave: 'a', rotulo: 'A' }]} linhas={[]} chaveLinha={() => ''} vazio={{ titulo: 'Nenhuma entrega', descricao: 'Registre a primeira.', acao: { rotulo: 'Registrar', aoClicar: () => {} } }} /></Card>
          <Card densidade="compacta" className={g.margemTopo}><DataTable colunas={[{ chave: 'a', rotulo: 'A' }]} linhas={[]} chaveLinha={() => ''} carregando /></Card>
          <Card densidade="compacta" className={g.margemTopo}>
            <DataTable
              aria-label="Tabela compacta com cabeçalho fixo"
              densidade="compacta"
              cabecalhoFixo
              colunas={[
                { chave: 'funcao', rotulo: 'Função' },
                { chave: 'cbo', rotulo: 'CBO', largura: '110px' },
                { chave: 'epis', rotulo: 'EPIs', alinhar: 'direita', largura: '80px' },
              ]}
              linhas={LINHAS_COMPACTAS}
              chaveLinha={(l) => l.id}
            />
          </Card>
        </div>
      </Secao>
      <Secao titulo="FormSection + FormGrid + FormRodape">
        <Card className={g.larguraTotal}>
          <FormSection titulo="Dados da entrega" numero={1} primeira>
            <FormGrid>
              <Campo span={6}><Field label="Funcionário"><Input /></Field></Campo>
              <Campo span={3}><Field label="Quantidade"><Input type="number" defaultValue="1" /></Field></Campo>
              <Campo span={3}><Field label="Entrega"><CampoData value="2026-09-07" onChange={() => {}} /></Field></Campo>
            </FormGrid>
          </FormSection>
          <FormSection titulo="Observações" numero={2}>
            <FormGrid><Campo><Field label="Texto"><Textarea /></Field></Campo></FormGrid>
          </FormSection>
          <FormRodape info="Ações concluídas exigem evidência fotográfica antes do encerramento."><Button>Cancelar</Button><Button appearance="primary">Salvar</Button></FormRodape>
        </Card>
      </Secao>
      <Secao titulo="ChipCheckboxGroup">
        <ChipCheckboxGroup aria-label="EPIs" opcoes={[{ id: 'a', rotulo: 'Capacete' }, { id: 'b', rotulo: 'Botina' }, { id: 'c', rotulo: 'Luva' }, { id: 'd', rotulo: 'Óculos' }]} selecionados={chipsDemo} aoMudar={setChipsDemo} />
      </Secao>
      {/* As tarefas seguintes acrescentam uma <Secao> por peça, nesta ordem:
          SeletorPesquisavel, ConfirmDialog, PainelLateral, KpiCard, DetailPageLayout,
          WorkflowActions, Gráficos. */}
    </div>
  );
}
