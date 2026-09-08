import { useEffect, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { makeStyles } from '@fluentui/react-components';
import { BuildingBank24Regular, ShieldCheckmark24Regular, DocumentCheckmark24Regular, DocumentError24Regular } from '@fluentui/react-icons';
import { useTipografia } from '../tokens/tipografia';
import { Card, PageHeader, Button, Input, StatusChip, nivelVencimento, tomDeVencimento, rotuloDeVencimento, EstadoVazio, Carregando, FeedbackInline, Abas, useAbaNaUrl, DataTable, FormSection, FormGrid, Campo, FormRodape, ChipCheckboxGroup, Field, Select, Textarea, CampoData, SeletorPesquisavel, useConfirmar, PainelLateral, KpiCard, DetailPageLayout, WorkflowActions, usePaletaGraficos, StatusDonutChart, RankingBarChart, TrendBarChart, TrendLineChart, ChipsField } from '../index';
import { Secao } from './Secao';

// Calculado uma vez no carregamento do módulo (não a cada render) para não disparar o alerta de
// pureza do oxlint sobre chamar Date.now() durante a renderização.
const validadeExemploVenceEmBreve = new Date(Date.now() + 8 * 86_400_000).toISOString().slice(0, 10);
// Massa de dados para exercitar densidade compacta + cabeçalho fixo + alinhamento de coluna.
const LINHAS_COMPACTAS = Array.from({ length: 25 }, (_, i) => ({ id: String(i + 1), funcao: `Função ${i + 1}`, cbo: `7${String(i).padStart(3, '0')}-05`, epis: (i * 7) % 13 }));
// Dados fixos e pequenos para os três gráficos da galeria — nada de aleatório, senão o snapshot
// visual muda a cada execução.
const RANKING_EXEMPLO = [
  { rotulo: 'Ponte Rio Cuiá', valor: 12, detalhe: '4 em tratamento' },
  { rotulo: 'Vila Nova', valor: 8 },
  { rotulo: 'Terminal Sul', valor: 5 },
  { rotulo: 'Anel Viário', valor: 3 },
];
const TENDENCIA_EXEMPLO = [
  { rotulo: 'Abr', valor: 6 }, { rotulo: 'Mai', valor: 9 }, { rotulo: 'Jun', valor: 4 },
  { rotulo: 'Jul', valor: 11 }, { rotulo: 'Ago', valor: 7 }, { rotulo: 'Set', valor: 5 },
];

const useGaleriaStyles = makeStyles({
  largura280: { width: '280px' },
  largura360: { width: '360px' },
  larguraTotal: { width: '100%' },
  margemTopo: { marginTop: '16px' },
  coluna: { display: 'flex', flexDirection: 'column', gap: '10px' },
  titulo: { marginBottom: '24px' },
  gradeKpi: { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(185px, 1fr))', gap: '16px', width: '100%' },
  linhaSwatches: { display: 'flex', gap: '8px', flexWrap: 'wrap' },
  swatch: { textAlign: 'center' },
  amostra: { width: '56px', height: '36px', borderRadius: '6px' },
});

// Galeria viva da camada ui/ (spec §6): cada peça em todos os estados, nos dois temas (use o botão
// da topbar). Só existe em desenvolvimento — ver a rota condicional em App.tsx. É a documentação.
export function GaleriaPage() {
  const tipo = useTipografia();
  const g = useGaleriaStyles();
  const paleta = usePaletaGraficos();
  const [abaPilar, setAbaPilar] = useState<'a' | 'b' | 'c'>('a');
  const [abaInterna, setAbaInterna] = useState<'p' | 'q'>('p');
  const [abaDemo, setAbaDemo] = useAbaNaUrl('demo', ['x', 'y', 'z'] as const, 'x');
  const [abertaDemo, setAbertaDemo] = useState<string | null>(null);
  const [chipsDemo, setChipsDemo] = useState<string[]>(['a']);
  const [funcDemo, setFuncDemo] = useState('');
  const { confirmar, dialogElement } = useConfirmar();
  const [ultimaConfirmacao, setUltimaConfirmacao] = useState<string | null>(null);
  // Só para os snapshots do Playwright — peças que só existem abertas.
  const [parametros] = useSearchParams();
  const abrir = parametros.get('abrir');
  const [painelAberto, setPainelAberto] = useState(abrir === 'painel');
  const [chipsTexto, setChipsTexto] = useState('Ponte Rio Cuiá; Vila Nova');
  const dialogoJaAberto = useRef(false);

  const [ultimaAcao, setUltimaAcao] = useState<string | null>(null);
  // Montagem única: o useRef segura a dupla invocação do StrictMode em desenvolvimento.
  useEffect(() => {
    if (abrir !== 'dialogo' || dialogoJaAberto.current) return;
    dialogoJaAberto.current = true;
    void confirmar('Excluir a entrega de João da Silva?');
  }, [abrir, confirmar]);
  return (
    <div>
      <h1 className={`${tipo.titulo} ${g.titulo}`}>Galeria da camada ui/</h1>
      <Secao id="tipografia" titulo="Tipografia">
        <div className={`${g.coluna} ${g.larguraTotal}`}>
          <span className={tipo.display}>1.248</span>
          <span className={tipo.titulo}>Entregas de EPI</span>
          <span className={tipo.subtitulo}>Plano de ação</span>
          <span className={tipo.corpo}>Andaime sem guarda-corpo no pavimento 3.</span>
          <span className={tipo.legenda}>Pedreiro, Ponte Rio Cuiá</span>
          <span className={tipo.micro}>Vence em breve</span>
        </div>
      </Secao>
      <Secao id="status-chip" titulo="StatusChip">
        <StatusChip tom="ok">Vigente</StatusChip>
        <StatusChip tom="atencao">Vence em 8 dias</StatusChip>
        <StatusChip tom="alerta">Vencido</StatusChip>
        <StatusChip tom="info">Aguardando assinatura</StatusChip>
        <StatusChip tom="neutro">Prevista</StatusChip>
        {(() => { const n = nivelVencimento('2099-01-01')!; return <StatusChip tom={tomDeVencimento(n)}>{rotuloDeVencimento(n)} (helper)</StatusChip>; })()}
        <StatusChip tom="alerta" pulsar="rapido">Vencido (pulsa)</StatusChip>
        <StatusChip tom="atencao" pulsar="leve">Vence em 8 dias (pulsa leve)</StatusChip>
      </Secao>
      <Secao id="card" titulo="Card">
        <Card className={g.largura280} titulo="Confortável" subtitulo="Padding xl">Conteúdo</Card>
        <Card densidade="compacta" titulo="Compacto" acoes={<Button size="small">Ação</Button>}>Conteúdo</Card>
      </Secao>
      <Secao id="page-header" titulo="PageHeader">
        <div className={g.larguraTotal}>
          <PageHeader titulo="Entregas de EPI" subtitulo="287 entregas ativas em 7 obras." status={<StatusChip tom="info">Em tratamento</StatusChip>} filtros={<Input placeholder="Buscar" />} acoes={<Button appearance="primary">Nova entrega</Button>} voltarPara="/ui-galeria" rotuloVoltar="Não conformidades" />
        </div>
      </Secao>
      <Secao id="estado-vazio" titulo="EstadoVazio">
        <Card className={g.largura360}><EstadoVazio titulo="Nenhuma entrega registrada" descricao="Registre a primeira entrega para começar o controle de EPI desta obra." acao={{ rotulo: 'Registrar entrega', aoClicar: () => {} }} /></Card>
        <Card className={g.largura360}><EstadoVazio variante="sem-resultado" titulo="Nenhum resultado" descricao="Tente outro termo." acao={{ rotulo: 'Limpar busca', aoClicar: () => {} }} /></Card>
        <Card className={g.largura360}><EstadoVazio variante="em-construcao" titulo="Documentos e Procedimentos" descricao="Módulo reservado no menu, ainda não construído." /></Card>
      </Secao>
      <Secao id="carregando" titulo="Carregando">
        <Card className={g.largura360}><Carregando variante="lista" /></Card>
        <div className={g.larguraTotal}><Carregando variante="kpi" /></div>
      </Secao>
      <Secao id="feedback-inline" titulo="FeedbackInline">
        <div className={g.larguraTotal}>
          <FeedbackInline tom="erro" aoFechar={() => {}}>Não foi possível salvar. Defina o responsável antes de enviar.</FeedbackInline>
          <FeedbackInline tom="info" acao={{ rotulo: 'Ver treinamento', aoClicar: () => {} }}>Campos de NR-06 preenchidos a partir do último treinamento.</FeedbackInline>
          <FeedbackInline tom="sucesso">Entrega registrada.</FeedbackInline>
        </div>
      </Secao>
      <Secao id="abas" titulo="Abas (a de módulo sincroniza com ?demo= na URL — troque e aperte F5)">
        <div className={g.larguraTotal}>
          <Abas nivel="pilar" abas={[{ valor: 'a', rotulo: 'PGR e GRO' }, { valor: 'b', rotulo: 'PCMSO' }, { valor: 'c', rotulo: 'Treinamentos', contador: 14 }]} valor={abaPilar} aoMudar={setAbaPilar} />
          <Abas nivel="modulo" abas={[{ valor: 'x', rotulo: 'Entregas' }, { valor: 'y', rotulo: 'Estoque' }, { valor: 'z', rotulo: 'Catálogo' }]} valor={abaDemo} aoMudar={setAbaDemo} />
          <Abas nivel="interno" abas={[{ valor: 'p', rotulo: 'Por obra' }, { valor: 'q', rotulo: 'Por função' }]} valor={abaInterna} aoMudar={setAbaInterna} />
        </div>
      </Secao>
      <Secao id="data-table" titulo="DataTable">
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
      <Secao id="formulario" titulo="FormSection + FormGrid + FormRodape">
        <Card className={g.larguraTotal}>
          <FormSection titulo="Dados da entrega" numero={1} primeira>
            <FormGrid>
              <Campo span={6}><Field label="Funcionário"><Input /></Field></Campo>
              <Campo span={3}><Field label="Quantidade"><Input type="number" defaultValue="1" /></Field></Campo>
              <Campo span={3}><Field label="Entrega"><CampoData value="2026-09-07" onChange={() => {}} /></Field></Campo>
            </FormGrid>
          </FormSection>
          {/* Faixas estreitas com controles crus do Fluent: cada Campo tem de respeitar o span
              declarado (2+3+3+2+2=12) em vez de o controle vazar por cima do vizinho — a largura
              intrínseca de Input/Select/Combobox passa de 190px, muito acima do span 2 (~112px).
              Caso real que motivou o minmax(0, 1fr) do FormGrid (piloto 2, NC). */}
          <FormSection titulo="Faixas estreitas" numero={2}>
            <FormGrid>
              <Campo span={2}><Field label="Tipo"><Select><option>Corretiva</option></Select></Field></Campo>
              <Campo span={3}><Field label="Descrição"><Input /></Field></Campo>
              <Campo span={3}><Field label="Responsável"><SeletorPesquisavel opcoes={[{ id: 'u1', rotulo: 'Ana Ribeiro' }]} valor="" aoMudar={() => {}} placeholder="Nenhum" /></Field></Campo>
              <Campo span={2}><Field label="Prioridade"><Select><option>Média</option></Select></Field></Campo>
              <Campo span={2}><Field label="Prazo"><CampoData value="" onChange={() => {}} /></Field></Campo>
            </FormGrid>
          </FormSection>
          <FormSection titulo="Observações" numero={3}>
            <FormGrid><Campo><Field label="Texto"><Textarea /></Field></Campo></FormGrid>
          </FormSection>
          <FormRodape info="Ações concluídas exigem evidência fotográfica antes do encerramento."><Button>Cancelar</Button><Button appearance="primary">Salvar</Button></FormRodape>
        </Card>
      </Secao>
      <Secao id="chip-checkbox-group" titulo="ChipCheckboxGroup">
        <ChipCheckboxGroup aria-label="EPIs" opcoes={[{ id: 'a', rotulo: 'Capacete' }, { id: 'b', rotulo: 'Botina' }, { id: 'c', rotulo: 'Luva' }, { id: 'd', rotulo: 'Óculos' }]} selecionados={chipsDemo} aoMudar={setChipsDemo} />
      </Secao>
      <Secao id="seletor-pesquisavel" titulo="SeletorPesquisavel (digite 'jo')">
        <div className={g.largura360}>
          <Field label="Funcionário">
            {/* opcaoVazia fica fixa no topo da lista: sem ela não há como voltar ao vazio depois de
                escolher alguém (achado do piloto 2). */}
            <SeletorPesquisavel aria-label="Funcionário" placeholder="Buscar entre 312 funcionários" opcaoVazia="Nenhum" valor={funcDemo} aoMudar={setFuncDemo}
              opcoes={[{ id: '1', rotulo: 'João da Silva', descricao: 'Pedreiro, Ponte Rio Cuiá' }, { id: '2', rotulo: 'Joana Martins', descricao: 'Carpinteira, Vila Nova' }, { id: '3', rotulo: 'José Almeida', descricao: 'Operador de guindaste' }, { id: '4', rotulo: 'Ana Carolina Reis', descricao: 'Eletricista' }]} />
          </Field>
        </div>
      </Secao>
      <Secao id="confirm-dialog" titulo="ConfirmDialog">
        <Button onClick={async () => { const ok = await confirmar('Excluir a entrega de João da Silva? Esta ação não pode ser desfeita.'); setUltimaConfirmacao(ok ? 'confirmou' : 'cancelou'); }}>Abrir destrutivo</Button>
        <Button onClick={async () => { const ok = await confirmar({ titulo: 'Enviar ao responsável', mensagem: 'A ocorrência será enviada a Carlos Mendes.', rotuloConfirmar: 'Enviar', tom: 'neutro' }); setUltimaConfirmacao(ok ? 'confirmou' : 'cancelou'); }}>Abrir neutro</Button>
        <span className={tipo.legenda}>Última resposta: {ultimaConfirmacao ?? '—'}</span>
        {dialogElement}
      </Secao>
      <Secao id="painel-lateral" titulo="PainelLateral">
        <Button appearance="primary" onClick={() => setPainelAberto(true)}>Abrir painel</Button>
        <PainelLateral aberto={painelAberto} aoFechar={() => setPainelAberto(false)} titulo="Nova entrega de EPI" subtitulo="A lista continua visível atrás." rodape={<><Button onClick={() => setPainelAberto(false)}>Cancelar</Button><Button appearance="primary" onClick={() => setPainelAberto(false)}>Registrar</Button></>}>
          <FormSection titulo="Quem recebe" numero={1} primeira><FormGrid><Campo><Field label="Funcionário"><Input /></Field></Campo></FormGrid></FormSection>
        </PainelLateral>
      </Secao>
      <Secao id="kpi-card" titulo="KpiCard (entrada escalonada — recarregue a página)">
        <div className={g.gradeKpi}>
          <KpiCard indice={0} tom="info" rotulo="Obras ativas" valor="7" deltas={[{ texto: '5 em andamento', tom: 'neutro' }]} icone={<BuildingBank24Regular />} />
          <KpiCard indice={1} tom="ok" rotulo="Conformidade de EPI" valor="94%" deltas={[{ texto: '287 entregas ativas', tom: 'neutro' }]} icone={<ShieldCheckmark24Regular />} />
          <KpiCard indice={2} tom="atencao" rotulo="Treinamentos em dia" valor="87%" deltas={[{ texto: '14 a vencer', tom: 'atencao', pulsar: 'leve' }, { texto: '6 vencidos', tom: 'alerta', pulsar: 'rapido' }]} icone={<DocumentCheckmark24Regular />} />
          <KpiCard indice={3} tom="alerta" rotulo="Não conformidades abertas" valor="12" deltas={[{ texto: '4 em tratamento', tom: 'alerta' }]} icone={<DocumentError24Regular />} />
          <KpiCard indice={4} tom="info" rotulo="Carregando" valor="" carregando />
        </div>
      </Secao>
      <Secao id="detail-page-layout" titulo="DetailPageLayout + WorkflowActions">
        <div className={g.larguraTotal}>
          <DetailPageLayout
            cabecalho={{ titulo: 'NC-2026-0042', subtitulo: 'Andaime sem guarda-corpo no pavimento 3.', status: <StatusChip tom="info">Em tratamento</StatusChip>, voltarPara: '/ui-galeria', rotuloVoltar: 'Não conformidades', acoes: <Button appearance="primary">Salvar</Button> }}
            lateral={<>
              <Card densidade="compacta" titulo="Resumo"><span className={tipo.corpo}>Ponte Rio Cuiá, pavimento 3</span></Card>
              <Card densidade="compacta" titulo="Ações disponíveis">
                <WorkflowActions acoes={[
                  { chave: 'concluir', rotulo: 'Concluir tratamento', descricao: 'Registra a evidência e envia para validação', tom: 'primario', formulario: <Field label="O que foi feito"><Textarea /></Field>, aoExecutar: () => setUltimaAcao('concluiu'), rotuloExecutar: 'Concluir e enviar' },
                  { chave: 'prazo', rotulo: 'Pedir mais prazo', descricao: 'Justifique e proponha nova data', formulario: <Field label="Nova data"><CampoData value="" onChange={() => {}} /></Field>, aoExecutar: () => setUltimaAcao('pediu prazo') },
                  { chave: 'devolver', rotulo: 'Devolver ao emitente', tom: 'destrutivo', formulario: <Field label="Motivo"><Textarea /></Field>, aoExecutar: () => setUltimaAcao('devolveu') },
                  { chave: 'encerrar', rotulo: 'Encerrar', descricao: 'Só após validação', habilitada: false, aoExecutar: () => {} },
                ]} />
                <span className={tipo.legenda}>Última ação: {ultimaAcao ?? '—'}</span>
              </Card>
            </>}
          >
            <Card><FormSection titulo="Registro" numero={1} primeira><FormGrid><Campo span={6}><Field label="Origem"><Input value="Inspeção de rotina" readOnly /></Field></Campo><Campo span={6}><Field label="Prioridade"><Input value="Alta" readOnly /></Field></Campo></FormGrid></FormSection></Card>
          </DetailPageLayout>
        </div>
      </Secao>
      <Secao id="paleta-graficos" titulo="Paleta de gráficos (segue o tema)">
        <div className={g.linhaSwatches}>
          {Object.entries(paleta).filter(([chave]) => chave !== 'serie').map(([chave, valor]) => (
            <div key={chave} className={g.swatch}>
              <div className={g.amostra} style={{ background: valor as string }} />
              <span className={tipo.micro}>{chave}</span>
            </div>
          ))}
        </div>
        <Card className={g.largura360} titulo="Aptidão ocupacional">
          <StatusDonutChart
            dados={[
              { rotulo: 'Aptos', valor: 256, cor: paleta.ok },
              { rotulo: 'Restrição', valor: 24, cor: paleta.atencao },
              { rotulo: 'Inaptos', valor: 7, cor: paleta.alerta },
            ]}
            legendaCentral="funcionários"
          />
        </Card>
      </Secao>
      <Secao id="graficos" titulo="Gráficos de ranking e tendência (cores da paleta)">
        <Card className={g.largura360} titulo="Obras com mais não conformidades">
          <RankingBarChart dados={RANKING_EXEMPLO} corPadrao={paleta.chrome} valorReferencia={8} />
        </Card>
        <Card className={g.largura360} titulo="Ocorrências por mês (barras)">
          <TrendBarChart dados={TENDENCIA_EXEMPLO} cor={paleta.atencao} />
        </Card>
        <Card className={g.largura360} titulo="Ocorrências por mês (área)">
          <TrendLineChart dados={TENDENCIA_EXEMPLO} cor={paleta.marca} />
        </Card>
      </Secao>
      <Secao id="chips-field" titulo="ChipsField (Enter, vírgula ou sair do campo confirma)">
        <div className={g.largura360}>
          <Field label="Unidades/obras abrangidas">
            <ChipsField value={chipsTexto} onChange={setChipsTexto} placeholder="Digite e aperte Enter" />
          </Field>
        </div>
      </Secao>
    </div>
  );
}
