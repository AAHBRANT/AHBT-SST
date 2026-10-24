import { useEffect, useMemo, useState } from 'react';
import { Button, Carregando, DataTable, EstadoVazio, FeedbackInline, Field, Legenda, PainelLateral, Select, StatusChip, type Coluna } from '@ui';
import { ArrowDownload24Regular } from '@fluentui/react-icons';
import { api, type FalhaReconhecimentoFacial } from '../../lib/api';

interface FalhasReconhecimentoFacialPainelProps {
  aberto: boolean;
  aoFechar: () => void;
}

function formatarDataHora(iso: string): string {
  return new Date(iso).toLocaleString('pt-BR', { timeZone: 'America/Sao_Paulo' });
}

function formatarConfianca(valor: number | null): string {
  return valor == null ? '—' : `${valor.toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%`;
}

// CSV com ";" e BOM: abre direto no Excel em português com acentos e vírgula decimal corretos.
function baixarCsv(linhas: FalhaReconhecimentoFacial[]) {
  const celula = (v: string) => `"${v.replace(/"/g, '""')}"`;
  const conteudo = [
    ['Data e hora', 'Obra', 'Funcionário', 'Motivo', 'Confiança (%)'].map(celula).join(';'),
    ...linhas.map((f) => [
      formatarDataHora(f.ocorridaEm),
      f.obraNome,
      f.trabalhadorNome ?? 'Não identificado',
      f.motivo,
      f.confiancaPercentual == null ? '' : f.confiancaPercentual.toLocaleString('pt-BR', { maximumFractionDigits: 1 }),
    ].map(celula).join(';')),
  ].join('\r\n');
  const url = URL.createObjectURL(new Blob(['﻿' + conteudo], { type: 'text/csv;charset=utf-8' }));
  const link = document.createElement('a');
  link.href = url;
  link.download = `falhas-reconhecimento-facial-${new Date().toISOString().slice(0, 10)}.csv`;
  link.click();
  URL.revokeObjectURL(url);
}

// Todas as falhas de reconhecimento facial do período, com a confiança de cada uma (12/10) — o que antes só
// dava para ver consultando o banco. A confiança exigida para assinar é 85%: entre 60% e 85% o Azure
// reconheceu a pessoa, mas abaixo do exigido (sinal de cadastro fraco).
export function FalhasReconhecimentoFacialPainel({ aberto, aoFechar }: FalhasReconhecimentoFacialPainelProps) {
  const [dias, setDias] = useState(30);
  const [obraId, setObraId] = useState('');
  const [falhas, setFalhas] = useState<FalhaReconhecimentoFacial[] | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (!aberto) return;
    let cancelado = false;
    setFalhas(null);
    setErro(null);
    api.trabalhadores
      .listarFalhasReconhecimentoFacial(dias)
      .then((l) => !cancelado && setFalhas(l))
      .catch((e) => !cancelado && setErro(e instanceof Error ? e.message : 'Não foi possível carregar as falhas.'));
    return () => {
      cancelado = true;
    };
  }, [aberto, dias]);

  const obras = useMemo(
    () => [...new Map((falhas ?? []).map((f) => [f.obraId, f.obraNome])).entries()].sort((a, b) => a[1].localeCompare(b[1])),
    [falhas],
  );
  const visiveis = useMemo(() => (falhas ?? []).filter((f) => !obraId || f.obraId === obraId), [falhas, obraId]);

  const colunas: Coluna<FalhaReconhecimentoFacial>[] = [
    { chave: 'quando', rotulo: 'Data e hora', render: (f) => formatarDataHora(f.ocorridaEm) },
    { chave: 'funcionario', rotulo: 'Funcionário', render: (f) => f.trabalhadorNome ?? <Legenda>Não identificado</Legenda> },
    { chave: 'obra', rotulo: 'Obra', render: (f) => f.obraNome },
    { chave: 'motivo', rotulo: 'Motivo', render: (f) => f.motivo },
    {
      chave: 'confianca',
      rotulo: 'Confiança',
      render: (f) => (
        <StatusChip tom={f.confiancaPercentual != null && f.confiancaPercentual >= 75 ? 'atencao' : 'alerta'}>
          {formatarConfianca(f.confiancaPercentual)}
        </StatusChip>
      ),
    },
  ];

  return (
    <PainelLateral
      aberto={aberto}
      aoFechar={aoFechar}
      largura="lg"
      titulo="Falhas do reconhecimento facial"
      subtitulo="Para assinar, a confiança precisa ser de pelo menos 85%."
      rodape={
        <Button icon={<ArrowDownload24Regular />} disabled={visiveis.length === 0} onClick={() => baixarCsv(visiveis)}>
          Exportar planilha (CSV)
        </Button>
      }
    >
      <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginBottom: 12 }}>
        <Field label="Período">
          <Select value={String(dias)} onChange={(_, d) => setDias(Number(d.value))}>
            <option value="7">Últimos 7 dias</option>
            <option value="30">Últimos 30 dias</option>
            <option value="90">Últimos 90 dias</option>
          </Select>
        </Field>
        <Field label="Obra">
          <Select value={obraId} onChange={(_, d) => setObraId(d.value)}>
            <option value="">Todas</option>
            {obras.map(([id, nome]) => <option key={id} value={id}>{nome}</option>)}
          </Select>
        </Field>
      </div>
      {erro && <FeedbackInline tom="erro">{erro}</FeedbackInline>}
      {!falhas && !erro && <Carregando />}
      {falhas && visiveis.length === 0 && <EstadoVazio titulo="Nenhuma falha no período" />}
      {visiveis.length > 0 && (
        <DataTable
          aria-label="Falhas do reconhecimento facial"
          colunas={colunas}
          linhas={visiveis}
          chaveLinha={(f) => f.id}
          densidade="compacta"
        />
      )}
    </PainelLateral>
  );
}
