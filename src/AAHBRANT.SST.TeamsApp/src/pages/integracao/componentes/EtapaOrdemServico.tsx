import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, Card, FeedbackInline, StatusChip } from '@ui';
import { Document20Regular, Info16Regular, Signature20Regular } from '@fluentui/react-icons';
import { api, type LinhaOsIntegracao, type OrdemServicoEmitida, type OsIntegracao } from '../../../lib/api';
import { AssinaturaOrdemServicoDialog } from '../../../components/assinatura/AssinaturaOrdemServicoDialog';
import { useVisualizadorPdf } from '../../../components/useVisualizadorPdf';
import { AvatarIniciais, TituloSecao } from './pecas';
import { useEstilosIntegracao } from './estilos';
import { dataHoraBrasil, primeiroNome } from './helpers';

interface Props {
  ordem: number;
  os: OsIntegracao | null;
  somenteLeitura: boolean;
  recarregarOs: () => Promise<void>;
  aoErro: (mensagem: string | null) => void;
}

// Etapa 5 — Ordem de Serviço (NR-01), mockup v4. Cada pessoa lê a própria OS (só a função dela) e assina à
// parte, por digital ou facial, depois o técnico. Reaproveita a emissão, o PDF e o diálogo de assinatura
// da aba "Ordem de Serviço" do perfil. Pendências que impedem emitir (ex.: sem ASO, de responsabilidade do
// RH) ficam agrupadas por motivo e não seguram as demais pessoas.
export function EtapaOrdemServico({ ordem, os, somenteLeitura, recarregarOs, aoErro }: Props) {
  const e = useEstilosIntegracao();
  const navigate = useNavigate();
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();
  const [ocupadoId, setOcupadoId] = useState<string | null>(null);
  const [assinando, setAssinando] = useState<{ os: OrdemServicoEmitida; trabalhadorId: string; nome: string } | null>(null);

  // "pendente" (RH) e "bloqueada" (PGR/engenheiro) não podem emitir: aparecem só nas pendências agrupadas.
  const prontas = (os?.pessoas ?? []).filter((p) => p.situacao !== 'pendente' && p.situacao !== 'bloqueada');
  const nAssinadas = prontas.filter((p) => p.situacao === 'assinada').length;

  function verPdf(p: LinhaOsIntegracao, osId: string, numero?: string | null) {
    void visualizar({
      titulo: `Ordem de Serviço ${numero ?? ''}`.trim(),
      nomeArquivo: `${numero ?? 'ordem-servico'}.pdf`,
      obter: () => api.ordensServico.baixarPdf(p.trabalhadorId, osId),
    });
  }

  async function ler(p: LinhaOsIntegracao) {
    try {
      setOcupadoId(p.trabalhadorId);
      aoErro(null);
      let osId = p.ordemServicoId ?? null;
      let numero = p.numero ?? null;
      if (!osId) {
        const emitida = await api.ordensServico.emitir(p.trabalhadorId);
        osId = emitida.id;
        numero = emitida.numero;
      }
      verPdf(p, osId, numero);
      await recarregarOs();
    } catch (err) {
      aoErro(err instanceof Error ? err.message : 'Não foi possível emitir a Ordem de Serviço.');
    } finally {
      setOcupadoId(null);
    }
  }

  async function assinar(p: LinhaOsIntegracao) {
    try {
      setOcupadoId(p.trabalhadorId);
      aoErro(null);
      const lista = await api.ordensServico.listar(p.trabalhadorId);
      const emitida = lista.find((x) => x.id === p.ordemServicoId) ?? lista.find((x) => x.vigente);
      if (!emitida) throw new Error('A Ordem de Serviço desta pessoa ainda não foi emitida.');
      setAssinando({ os: emitida, trabalhadorId: p.trabalhadorId, nome: p.nome });
    } catch (err) {
      aoErro(err instanceof Error ? err.message : 'Não foi possível abrir a assinatura da Ordem de Serviço.');
    } finally {
      setOcupadoId(null);
    }
  }

  return (
    <Card
      titulo={<TituloSecao numero={ordem} estado={somenteLeitura ? 'feito' : 'atual'}>Ordem de Serviço (NR-01)</TituloSecao>}
      acoes={<StatusChip tom={somenteLeitura ? 'ok' : 'atencao'}>{somenteLeitura ? 'Concluída' : 'Em andamento'}</StatusChip>}
      subtitulo="Cada pessoa lê a própria OS e assina à parte. A OS assinada vale como NR-01, em registro isolado no perfil, sem certificado."
    >
      <FeedbackInline tom="info">
        <b>Conteúdo da OS: só a função de cada pessoa</b>, com as atividades, os riscos e os EPIs dela. A assinatura é por digital ou facial,
        separada das assinaturas dos cursos.
      </FeedbackInline>

      {!os ? (
        <div className={e.mudo}>Carregando as Ordens de Serviço…</div>
      ) : (
        <>
          {os.pendenciasAgrupadas.length > 0 && (
            <>
              <div className={e.tituloMini} style={{ marginTop: 0 }}>Bloqueadas (agrupadas por motivo)</div>
              {os.pendenciasAgrupadas.map((g) => (
                <div key={g.tipo} className={e.grupo}>
                  <div>
                    <div>
                      <StatusChip tom="alerta">Bloqueada</StatusChip> <b>{g.titulo}</b>
                    </div>
                    <div className={e.grupoNomes}>
                      <b>{g.nomes.length} {g.nomes.length === 1 ? 'pessoa' : 'pessoas'}:</b> {g.nomes.join(', ')}
                    </div>
                    <div className={e.mudo} style={{ marginTop: 2 }}>
                      {g.quemResolve ? `Quem resolve: ${g.quemResolve}. ` : ''}Não trava a conclusão da integração.
                    </div>
                  </div>
                  <Button
                    size="medium"
                    className={e.btnMedio}
                    onClick={() => navigate(`/pessoas/${g.trabalhadoresIds[0] ?? ''}`)}
                  >
                    Ver detalhes
                  </Button>
                </div>
              ))}
            </>
          )}

          <div className={e.tituloMini}>Prontas para leitura e assinatura ({nAssinadas}/{prontas.length})</div>
          {prontas.length === 0 && <div className={e.mudo}>Nenhuma pessoa está pronta para emitir a Ordem de Serviço agora.</div>}
          {prontas.map((p) => {
            const ocupado = ocupadoId === p.trabalhadorId;
            return (
              <div key={p.trabalhadorId} className={e.linhaParticipante}>
                <AvatarIniciais nome={p.nome} pequeno />
                <div>
                  <b>{p.nome}</b> <span className={e.funcaoPessoa}>{p.funcao}</span>
                  <div style={{ marginTop: 4 }}>
                    {p.situacao === 'assinada' ? (
                      <StatusChip tom="ok">Assinada{p.assinadaEmUtc ? ` em ${dataHoraBrasil(p.assinadaEmUtc)}` : ''}</StatusChip>
                    ) : (
                      <StatusChip tom="atencao">
                        {p.situacao === 'aguardandoTecnico' ? 'Aguardando o técnico' : p.situacao === 'aguardandoFuncionario' ? 'Aguardando assinatura' : 'Aguardando leitura'}
                      </StatusChip>
                    )}
                  </div>
                  <div className={e.mudo} style={{ marginTop: 4 }}>Conteúdo da OS: só a função de {primeiroNome(p.nome)} ({p.funcao})</div>
                </div>
                {p.situacao === 'assinada' && p.ordemServicoId ? (
                  <Button className={e.btnMedio} icon={<Document20Regular />} onClick={() => verPdf(p, p.ordemServicoId!, p.numero)}>
                    Ver PDF
                  </Button>
                ) : !somenteLeitura && (p.situacao === 'aguardandoFuncionario' || p.situacao === 'aguardandoTecnico') ? (
                  <Button appearance="primary" className={e.btnMedio} icon={<Signature20Regular />} disabled={ocupado} onClick={() => void assinar(p)}>
                    Assinar a OS
                  </Button>
                ) : !somenteLeitura ? (
                  <Button appearance="primary" className={e.btnMedio} icon={<Document20Regular />} disabled={ocupado} onClick={() => void ler(p)}>
                    Ler a OS com {primeiroNome(p.nome)}
                  </Button>
                ) : null}
              </div>
            );
          })}
        </>
      )}

      <div className={e.selo}>
        <Info16Regular />
        <span>Ao assinar a OS, o perfil da pessoa recebe o selo <b>Integração realizada</b> (sem certificado).</span>
      </div>

      {assinando && (
        <AssinaturaOrdemServicoDialog
          open
          onClose={() => {
            setAssinando(null);
            void recarregarOs();
          }}
          ordemServico={assinando.os}
          trabalhadorId={assinando.trabalhadorId}
          trabalhadorNome={assinando.nome}
        />
      )}
      {dialogoVisualizador}
    </Card>
  );
}
