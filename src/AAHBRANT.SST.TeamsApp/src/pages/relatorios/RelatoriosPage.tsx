import { useCallback, useEffect, useState } from 'react';
import {
  BotaoAcao,
  Card,
  DataTable,
  FeedbackInline,
  Field,
  Legenda,
  PageHeader,
  Select,
  StatusChip,
  type Coluna,
} from '@ui';
import { Document24Regular, Image24Regular } from '@fluentui/react-icons';
import { api, tipoRelatorioLabel, TipoRelatorio, type RelatorioLista } from '../../lib/api';
import { useVisualizadorPdf } from '../../components/useVisualizadorPdf';

function formatarDataHora(iso: string): string {
  return new Date(iso).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short', timeZone: 'America/Sao_Paulo' });
}

function nomeBase(r: RelatorioLista): string {
  const data = new Date(r.geradoEm).toISOString().slice(0, 10);
  return `${tipoRelatorioLabel[r.tipo] ?? 'Relatorio'}_${data}`.replace(/\s+/g, '-');
}

// Histórico dos relatórios gerados pelo sistema (lista de presença às 08:00, boletim semanal, ocorrência). Cada um
// tem a imagem que foi ao Telegram e, quando existe, o PDF de detalhe. Quem não tem acesso à obra não vê o relatório.
export function RelatoriosPage() {
  const [lista, setLista] = useState<RelatorioLista[]>([]);
  const [tipo, setTipo] = useState('');
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();

  const carregar = useCallback(async () => {
    setCarregando(true);
    setErro(null);
    try {
      setLista(await api.relatorios.listar(tipo ? Number(tipo) : undefined));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível carregar os relatórios.');
    } finally {
      setCarregando(false);
    }
  }, [tipo]);

  useEffect(() => {
    void carregar();
  }, [carregar]);

  const colunas: Coluna<RelatorioLista>[] = [
    { chave: 'quando', rotulo: 'Gerado em', render: (r) => formatarDataHora(r.geradoEm) },
    { chave: 'tipo', rotulo: 'Tipo', render: (r) => <StatusChip tom="info">{tipoRelatorioLabel[r.tipo] ?? 'Relatório'}</StatusChip> },
    { chave: 'obra', rotulo: 'Obra', render: (r) => r.obraNome ?? 'Todas as obras' },
    {
      chave: 'resumo',
      rotulo: 'Resumo',
      render: (r) => (
        <div>
          <div>{r.titulo}</div>
          <Legenda>{r.resumo}</Legenda>
        </div>
      ),
    },
  ];

  return (
    <div>
      <PageHeader titulo="Relatórios" />
      <div style={{ display: 'grid', gap: 16 }}>
        {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
        <Card titulo="Relatórios gerados">
          <div style={{ display: 'grid', gap: 12 }}>
            <Legenda>
              A lista de presença sai todo dia às 08:00 e o boletim semanal toda segunda às 07:00. Cada relatório fica aqui com a
              imagem e o PDF.
            </Legenda>
            <div style={{ maxWidth: 260 }}>
              <Field label="Tipo">
                <Select value={tipo} onChange={(_, d) => setTipo(d.value)}>
                  <option value="">Todos</option>
                  <option value={TipoRelatorio.ListaPresencaDds}>{tipoRelatorioLabel[TipoRelatorio.ListaPresencaDds]}</option>
                  <option value={TipoRelatorio.BoletimSemanal}>{tipoRelatorioLabel[TipoRelatorio.BoletimSemanal]}</option>
                  <option value={TipoRelatorio.Ocorrencia}>{tipoRelatorioLabel[TipoRelatorio.Ocorrencia]}</option>
                </Select>
              </Field>
            </div>
            <DataTable
              aria-label="Relatórios gerados"
              colunas={colunas}
              linhas={lista}
              chaveLinha={(r) => r.id}
              carregando={carregando}
              vazio={{
                titulo: 'Nenhum relatório ainda',
                descricao: 'O primeiro aparece depois do próximo disparo das 08:00, quando houver um DDS encerrado.',
              }}
              acoesLinha={(r) => (
                <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                  <BotaoAcao
                    tom="ver"
                    size="small"
                    icon={<Image24Regular />}
                    aria-label={`Ver imagem: ${r.titulo}`}
                    onClick={() => void visualizar({ titulo: r.titulo, nomeArquivo: `${nomeBase(r)}.png`, obter: () => api.relatorios.baixarImagem(r.id) })}
                  >
                    Ver imagem
                  </BotaoAcao>
                  {r.temPdf && (
                    <BotaoAcao
                      tom="ver"
                      size="small"
                      icon={<Document24Regular />}
                      aria-label={`Ver PDF: ${r.titulo}`}
                      onClick={() => void visualizar({ titulo: r.titulo, nomeArquivo: `${nomeBase(r)}.pdf`, obter: () => api.relatorios.baixarPdf(r.id) })}
                    >
                      Ver PDF
                    </BotaoAcao>
                  )}
                </div>
              )}
            />
          </div>
        </Card>
      </div>
      {dialogoVisualizador}
    </div>
  );
}
