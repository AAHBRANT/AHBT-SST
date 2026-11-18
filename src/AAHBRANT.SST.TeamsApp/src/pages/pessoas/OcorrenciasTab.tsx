import { useNavigate } from 'react-router-dom';
import { Card, DataTable, StatusChip, type Coluna, type Tom } from '@ui';
import {
  PapelEnvolvido,
  gravidadeAcidenteLabel,
  statusAcidenteLabel,
  tipoOcorrenciaLabel,
  type OcorrenciaPerfil,
} from '../../lib/api';
import { dataBr } from '../../lib/datas';

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
  const navigate = useNavigate();
  const colunas: Coluna<OcorrenciaPerfil>[] = [
    {
      chave: 'tipo',
      rotulo: 'Tipo',
      render: (o) =>
        o.papel === PapelEnvolvido.Testemunha ? (
          <span style={{ display: 'inline-flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
            {tipoOcorrenciaLabel[o.tipo]} <StatusChip tom="info">Testemunha</StatusChip>
          </span>
        ) : (
          tipoOcorrenciaLabel[o.tipo]
        ),
    },
    { chave: 'data', rotulo: 'Data', render: (o) => dataBr(o.data) },
    { chave: 'local', rotulo: 'Local' },
    {
      chave: 'gravidade',
      rotulo: 'Gravidade',
      render: (o) =>
        o.papel === PapelEnvolvido.Testemunha ? (
          '—'
        ) : (
        <StatusChip tom={tomPorGravidade[o.gravidade] ?? 'neutro'}>{gravidadeAcidenteLabel[o.gravidade]}</StatusChip>
        ),
    },
    { chave: 'diasAfastado', rotulo: 'Dias afastado', render: (o) => (o.papel !== PapelEnvolvido.Testemunha && o.houveAfastamento ? o.diasAfastamento ?? 0 : '—') },
    { chave: 'status', rotulo: 'Status', render: (o) => statusAcidenteLabel[o.status] },
  ];

  return (
    <Card titulo="Ocorrências registradas">
      <DataTable
        aria-label="Ocorrências registradas"
        colunas={colunas}
        linhas={ocorrencias}
        chaveLinha={(o) => o.id}
        aoClicarLinha={(o) => navigate(`/acidentes/${o.id}`)}
        vazio={{ titulo: 'Nenhuma ocorrência registrada para este funcionário.' }}
      />
    </Card>
  );
}
