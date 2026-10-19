import { useEffect, useState } from 'react';
import { Button, Card, DataTable, FeedbackInline, KpiCard, StatusChip, Text, tokensUi, type Coluna } from '@ui';
import { ArrowClockwise24Regular, ArrowDownload24Regular, Alert24Regular, Mail24Regular, DocumentError24Regular } from '@fluentui/react-icons';
import { api, type PreviaAvisoDestinatario, type PreviaAvisoDocumentosVencidos } from '../../lib/api';

// A API devolve datas sem fuso ("2026-10-20T00:00:00"): formata pelo texto para não deslocar o dia.
function formatarData(iso: string) {
  return iso.slice(0, 10).split('-').reverse().join('/');
}

function formatarDataEnvio(iso: string) {
  const [ano, mes, dia] = iso.slice(0, 10).split('-').map(Number);
  return new Date(ano, mes - 1, dia).toLocaleDateString('pt-BR', { weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric' });
}

// CSV com ";" e BOM: abre direto no Excel em português com acentos corretos. Uma linha por pessoa × documento.
function baixarCsv(previa: PreviaAvisoDocumentosVencidos) {
  const celula = (v: string) => `"${v.replace(/"/g, '""')}"`;
  const conteudo = [
    ['Destinatário', 'E-mail', 'Por que recebe', 'Aviso no Teams', 'Documento', 'Nome do documento', 'Obra', 'Vencimento', 'Dias vencido']
      .map(celula)
      .join(';'),
    ...previa.destinatarios.flatMap((d) =>
      d.documentos.map((doc) =>
        [
          d.nome,
          d.email,
          d.motivos.join(' | '),
          d.recebeSininho ? 'Sim' : `Não (${d.motivoSemSininho ?? ''})`,
          doc.tipo,
          doc.nome,
          doc.obra,
          formatarData(doc.vencimento),
          String(doc.diasVencido),
        ]
          .map(celula)
          .join(';')
      )
    ),
  ].join('\r\n');
  const url = URL.createObjectURL(new Blob(['﻿' + conteudo], { type: 'text/csv;charset=utf-8' }));
  const link = document.createElement('a');
  link.href = url;
  link.download = `previa-aviso-documentos-vencidos-${previa.dataEnvio.slice(0, 10)}.csv`;
  link.click();
  URL.revokeObjectURL(url);
}

// Prévia do aviso diário de PGR/PCMSO vencido (pedido do usuário, 19/10/2026): quem recebe no próximo disparo
// (seg–sex, 07:00), por qual vínculo e quais documentos — para conferir antes de ligar o envio no Worker.
// Só leitura: nada é enviado daqui.
export function PreviaAvisoDocumentosVencidosTab() {
  const [previa, setPrevia] = useState<PreviaAvisoDocumentosVencidos | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);
  const [expandido, setExpandido] = useState<string | null>(null);

  async function carregar() {
    try {
      setCarregando(true);
      setErro(null);
      setPrevia(await api.alertas.previaAvisoDocumentosVencidos());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar a prévia do aviso.');
    } finally {
      setCarregando(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  const destinatarios = previa?.destinatarios ?? [];
  const totalDocumentos = new Set(destinatarios.flatMap((d) => d.documentos.map((doc) => `${doc.tipo}|${doc.nome}|${doc.obra}`))).size;
  const semSininho = destinatarios.filter((d) => !d.recebeSininho).length;

  const colunas: Coluna<PreviaAvisoDestinatario>[] = [
    {
      chave: 'nome',
      rotulo: 'Destinatário',
      render: (d) => (
        <div>
          <Text weight="semibold">{d.nome}</Text>
          <div style={{ fontSize: 12, opacity: 0.75 }}>{d.email}</div>
        </div>
      ),
    },
    {
      chave: 'motivos',
      rotulo: 'Por que recebe',
      render: (d) => (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 2, fontSize: 13 }}>
          {d.motivos.length === 0 ? <span>—</span> : d.motivos.map((m) => <span key={m}>{m}</span>)}
        </div>
      ),
    },
    {
      chave: 'documentos',
      rotulo: 'Documentos',
      alinhar: 'centro',
      largura: '110px',
      render: (d) => <StatusChip tom="neutro">{d.documentos.length}</StatusChip>,
    },
    {
      chave: 'canais',
      rotulo: 'Canais',
      largura: '200px',
      render: (d) => (
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: tokensUi.espaco.xs }}>
          <StatusChip tom="ok">E-mail</StatusChip>
          {d.recebeSininho ? (
            <StatusChip tom="ok">Teams</StatusChip>
          ) : (
            <span title={d.motivoSemSininho ?? undefined}>
              <StatusChip tom="neutro">Sem Teams</StatusChip>
            </span>
          )}
        </div>
      ),
    },
  ];

  return (
    <div>
      {erro && <FeedbackInline tom="erro">{erro}</FeedbackInline>}

      <FeedbackInline tom="info">
        Esta tela só mostra quem <strong>receberia</strong> o aviso. Nada é enviado daqui. O envio automático (segunda a
        sexta, às 7h) só começa depois que o suporte técnico ligar o aviso no servidor.
      </FeedbackInline>

      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
          gap: tokensUi.espaco.lg,
          marginBottom: tokensUi.espaco.lg,
        }}
      >
        <KpiCard indice={0} tom="info" icone={<Mail24Regular />} rotulo="Destinatários" valor={destinatarios.length} carregando={carregando} />
        <KpiCard indice={1} tom="alerta" icone={<DocumentError24Regular />} rotulo="Documentos vencidos" valor={totalDocumentos} carregando={carregando} />
        <KpiCard
          indice={2}
          tom={semSininho > 0 ? 'atencao' : 'ok'}
          icone={<Alert24Regular />}
          rotulo="Sem aviso no Teams (só e-mail)"
          valor={semSininho}
          carregando={carregando}
        />
      </div>

      <Card
        densidade="compacta"
        titulo={previa ? `Próximo envio: ${formatarDataEnvio(previa.dataEnvio)}, às 7h` : 'Próximo envio'}
        subtitulo="Clique numa pessoa para ver os documentos que vão no e-mail dela."
        acoes={
          <div style={{ display: 'flex', gap: tokensUi.espaco.sm }}>
            <Button icon={<ArrowClockwise24Regular />} onClick={carregar} disabled={carregando}>
              Atualizar
            </Button>
            <Button
              appearance="primary"
              icon={<ArrowDownload24Regular />}
              disabled={!previa || destinatarios.length === 0}
              onClick={() => previa && baixarCsv(previa)}
            >
              Exportar planilha (CSV)
            </Button>
          </div>
        }
      >
        <DataTable
          aria-label="Destinatários do aviso de documentos vencidos"
          densidade="compacta"
          colunas={colunas}
          linhas={destinatarios}
          chaveLinha={(d) => d.email}
          carregando={carregando}
          vazio={{
            titulo: 'Ninguém recebe aviso no próximo envio',
            descricao: 'Não há PGR nem PCMSO vencido em obra ativa, ou as obras com documento vencido não têm técnico, engenheiro ou gestor vinculado.',
          }}
          aoClicarLinha={(d) => setExpandido((atual) => (atual === d.email ? null : d.email))}
          expansivel={{
            aberta: (d) => d.email === expandido,
            render: (d) => (
              <div style={{ padding: `${tokensUi.espaco.md} ${tokensUi.espaco.lg}` }}>
                {!d.recebeSininho && d.motivoSemSininho && (
                  <div style={{ fontSize: 13, marginBottom: tokensUi.espaco.sm }}>
                    <strong>Sem aviso no Teams:</strong> {d.motivoSemSininho}. A pessoa recebe só o e-mail.
                  </div>
                )}
                <DataTable
                  aria-label={`Documentos do aviso de ${d.nome}`}
                  densidade="compacta"
                  linhas={d.documentos}
                  chaveLinha={(doc) => `${doc.tipo}|${doc.nome}|${doc.obra}`}
                  colunas={[
                    { chave: 'tipo', rotulo: 'Documento', largura: '100px', render: (doc) => <strong>{doc.tipo}</strong> },
                    { chave: 'nome', rotulo: 'Nome' },
                    { chave: 'obra', rotulo: 'Obra' },
                    { chave: 'vencimento', rotulo: 'Venceu em', largura: '120px', render: (doc) => formatarData(doc.vencimento) },
                    {
                      chave: 'diasVencido',
                      rotulo: 'Atraso',
                      largura: '120px',
                      render: (doc) => <StatusChip tom="alerta" pulsar="leve">{doc.diasVencido} dia(s)</StatusChip>,
                    },
                  ]}
                />
              </div>
            ),
          }}
        />
      </Card>
    </div>
  );
}
