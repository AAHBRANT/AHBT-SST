import { Card, DataTable, StatusChip, type Coluna, type Tom } from '@ui';
import {
  gravidadeAcidenteLabel,
  statusAcidenteLabel,
  tipoOcorrenciaLabel,
  type OcorrenciaPerfil,
} from '../../lib/api';

// Gravidade 1-5: mapeamento por julgamento semântico (guia de conversão da Onda 2), não posicional —
// StatusChip só tem 5 tons e nenhum equivale a "severe" separado de "danger".
const tomPorGravidade: Record<number, Tom> = {
  1: 'ok',
  2: 'atencao',
  3: 'alerta',
  4: 'alerta',
  5: 'alerta',
};

// Sub-aba somente-leitura de TrabalhadorDetalhePage (aba "Ocorrências"). Camada ui/ (Onda 2, Task 1):
// Card com título de seção + DataTable; sem formulário e sem erro próprio (dados vêm prontos do
// perfil carregado pela página-mãe), por isso nenhuma conversão de FeedbackInline aqui.
export function OcorrenciasTab({ ocorrencias }: { ocorrencias: OcorrenciaPerfil[] }) {
  const colunas: Coluna<OcorrenciaPerfil>[] = [
    { chave: 'tipo', rotulo: 'Tipo', render: (o) => tipoOcorrenciaLabel[o.tipo] },
    { chave: 'data', rotulo: 'Data', render: (o) => o.data?.slice(0, 10) ?? '' },
    { chave: 'local', rotulo: 'Local' },
    {
      chave: 'gravidade',
      rotulo: 'Gravidade',
      render: (o) => (
        <StatusChip tom={tomPorGravidade[o.gravidade] ?? 'neutro'}>{gravidadeAcidenteLabel[o.gravidade]}</StatusChip>
      ),
    },
    { chave: 'diasAfastado', rotulo: 'Dias afastado', render: (o) => (o.houveAfastamento ? o.diasAfastamento ?? 0 : '—') },
    { chave: 'status', rotulo: 'Status', render: (o) => statusAcidenteLabel[o.status] },
  ];

  return (
    <Card titulo="Ocorrências registradas">
      <DataTable
        aria-label="Ocorrências registradas"
        colunas={colunas}
        linhas={ocorrencias}
        chaveLinha={(o) => o.id}
        vazio={{ titulo: 'Nenhuma ocorrência registrada para este funcionário.' }}
      />
    </Card>
  );
}
