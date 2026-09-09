// Porta única da camada ui/ (spec 2026-09-07 §2). Páginas importam daqui, nunca do Fluent direto.
export * from './tokens/tokens';
export * from './tokens/tons';
export * from './tokens/tipografia';
export * from './tokens/movimento';

// Primitivos Fluent que não ganham wrapper (spec §2.2) — re-exportados para a página não precisar
// importar @fluentui/react-components.
export {
  Avatar, Button, Checkbox, Field, Input, Select, Spinner, Text, Textarea, Tooltip,
  // Radio/RadioGroup: mesmo espírito do resto da lista (a11y já correta, sem hex/estilo próprio) —
  // faltava na lista original da spec §2.2 por não ter uso ainda; achado na Onda 2 Task 18
  // (QuestionarioAplicabilidadeTab.tsx, único consumidor no app hoje).
  Radio, RadioGroup,
} from '@fluentui/react-components';

// Exceção pontual (spec §5.1): primitivos de tabela crus, só para grades que genuinamente não são
// lista de dados — DataTable não serve. Ex.: MatrizRiscoTab.tsx (Onda 2 Task 13) — heatmap
// Probabilidade × Severidade onde cada célula é um <Select>, sem noção de "linha = item com colunas
// fixas"; ControleAcessoTab.tsx (Onda 2 Task 17) — matriz módulo × escopo com bulk-toggle por
// coluna, onde a própria "linha" carrega controles interativos que colidiriam com o clique de
// expandir/recolher de `DataTable`. Continuam proibidos para qualquer caso de lista — esse é
// sempre DataTable. Não é wrapper — é o mesmo Table do Fluent, só reexportado por aqui.
export {
  Table, TableBody, TableCell, TableHeader, TableHeaderCell, TableRow,
} from '@fluentui/react-components';

// Peças já existentes, ainda no lugar antigo até a Onda 3.
export { CampoData } from '../components/CampoData';
export { ChipsField } from '../components/ChipsField';

export { StatusChip, type StatusChipProps } from './primitivos/StatusChip/StatusChip';
export * from './primitivos/StatusChip/vencimento';

export { Card, type CardProps } from './compostos/Card/Card';
export { PageHeader, type PageHeaderProps } from './compostos/PageHeader/PageHeader';

export { EstadoVazio, type EstadoVazioProps } from './primitivos/EstadoVazio/EstadoVazio';
export { Carregando, type CarregandoProps } from './primitivos/Carregando/Carregando';
export { FeedbackInline, type FeedbackInlineProps } from './primitivos/FeedbackInline/FeedbackInline';
export { Legenda, type LegendaProps } from './primitivos/Legenda/Legenda';
export { BarraProgresso, type BarraProgressoProps } from './primitivos/BarraProgresso/BarraProgresso';

export { Abas, type AbasProps, type AbaItem } from './compostos/Abas/Abas';
export { useAbaNaUrl } from './compostos/Abas/useAbaNaUrl';

export { DataTable, type DataTableProps, type Coluna } from './compostos/DataTable/DataTable';

export { FormSection, FormRodape } from './compostos/Formulario/FormSection';
export { FormGrid, Campo } from './compostos/Formulario/FormGrid';
export { ChipCheckboxGroup, type ChipCheckboxGroupProps } from './primitivos/ChipCheckboxGroup/ChipCheckboxGroup';

export { SeletorPesquisavel, type SeletorPesquisavelProps, type OpcaoSeletor } from './primitivos/SeletorPesquisavel/SeletorPesquisavel';
export { useConfirmar, type OpcoesConfirmacao } from './primitivos/ConfirmDialog/useConfirmar';

export { PainelLateral, type PainelLateralProps } from './compostos/PainelLateral/PainelLateral';
export { KpiCard, type KpiCardProps } from './compostos/KpiCard/KpiCard';

export { DetailPageLayout, type DetailPageLayoutProps } from './layout/DetailPageLayout/DetailPageLayout';
export { WorkflowActions, type WorkflowActionsProps, type AcaoWorkflow } from './layout/WorkflowActions/WorkflowActions';

export * from './graficos';
