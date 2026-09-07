import { makeStyles } from '@fluentui/react-components';
import { useTipografia } from '../tokens/tipografia';
import { Card, PageHeader, Button, Input, StatusChip, nivelVencimento, tomDeVencimento, rotuloDeVencimento } from '../index';
import { Secao } from './Secao';

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
      {/* As tarefas seguintes acrescentam uma <Secao> por peça, nesta ordem: EstadoVazio, Carregando,
          FeedbackInline, Abas, DataTable, FormSection/FormGrid, ChipCheckboxGroup, SeletorPesquisavel,
          ConfirmDialog, PainelLateral, KpiCard, DetailPageLayout, WorkflowActions, Gráficos. */}
    </div>
  );
}
