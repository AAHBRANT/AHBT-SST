import { useEffect, useState } from 'react';
import { Card, FeedbackInline, StatusChip } from '@ui';
import { Camera16Regular, Checkmark16Regular } from '@fluentui/react-icons';
import { api, type IntegracaoAtendimentoDetalhe, type OsIntegracao } from '../../../lib/api';
import { AvatarIniciais, TituloSecao } from './pecas';
import { mergeClasses, useEstilosIntegracao } from './estilos';
import { nomesCurtos, participantesAtivos, rotuloEtapa } from './helpers';

interface Props {
  integracao: IntegracaoAtendimentoDetalhe;
  os: OsIntegracao | null;
}

function hora(iso?: string | null): string | null {
  if (!iso) return null;
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? null : d.toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short', timeZone: 'America/Sao_Paulo' });
}

function lista(nomes: string[]): string {
  if (nomes.length <= 1) return nomes.join('');
  return `${nomes.slice(0, -1).join(', ')} e ${nomes[nomes.length - 1]}`;
}

// Tela "Integração concluída" (mockup v4): resumo por etapa (certificados por NR e OS assinadas), as 3
// fotos de cada etapa com os horários e o selo "Integração realizada" por pessoa, que não é certificado.
export function IntegracaoConcluida({ integracao, os }: Props) {
  const e = useEstilosIntegracao();
  const [horariosFotos, setHorariosFotos] = useState<Record<string, string[]>>({});

  const etapasCurso = [...integracao.etapas].sort((a, b) => a.ordem - b.ordem).filter((x) => x.tipo !== 'ordemServico');
  const etapaOs = integracao.etapas.find((x) => x.tipo === 'ordemServico');

  useEffect(() => {
    let ativo = true;
    void Promise.all(
      etapasCurso
        .filter((et) => et.sessaoTreinamentoId && et.status === 'concluida' && et.participantes.length > 0)
        .map(async (et) => {
          try {
            const det = await api.sessoesTreinamento.obterDetalhe(et.sessaoTreinamentoId!);
            const horas = det.fotosEvidencia
              .slice()
              .sort((a, b) => a.ordem - b.ordem)
              .map((f) => hora(f.dadosFoto?.capturadaEm))
              .filter((h): h is string => !!h);
            return [et.id, horas] as const;
          } catch {
            return [et.id, [] as string[]] as const;
          }
        }),
    ).then((pares) => {
      if (ativo) setHorariosFotos(Object.fromEntries(pares));
    });
    return () => {
      ativo = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [integracao.id]);

  const totalCertificados = etapasCurso.reduce((t, et) => t + participantesAtivos(et).length, 0);
  const osAssinadas = os?.pessoas.filter((p) => p.situacao === 'assinada') ?? [];
  const osPendentes = os?.pessoas.filter((p) => p.situacao !== 'assinada') ?? [];
  const concluidaEm = hora(integracao.concluidaEmUtc);

  return (
    <>
      <Card
        titulo={<TituloSecao numero="" estado="feito">Integração concluída</TituloSecao>}
        acoes={<StatusChip tom="ok">{totalCertificados} certificados · {osAssinadas.length} OS</StatusChip>}
      >
        <FeedbackInline tom="sucesso">
          <b>Integração concluída{concluidaEm ? ` em ${concluidaEm.replace(', ', ' às ')}` : ''}</b> por {integracao.responsavelNome}. Cada curso teve a
          assinatura biométrica de cada participante, e a OS foi assinada à parte.
        </FeedbackInline>
        <div className={e.tituloMini}>Resumo por etapa</div>
        {etapasCurso.map((et) => {
          const rot = rotuloEtapa(et.tipo);
          const n = participantesAtivos(et).length;
          const semCert = et.participantes.filter((p) => p.naoAssinou);
          if (n === 0) {
            return (
              <div key={et.id} className={e.linhaResumo}>
                <span className={e.numero} aria-hidden="true">–</span>
                <div>
                  <b>{rot.codigo}</b>
                  <div className={e.mudo}>Ninguém precisava desta etapa. Pulada.</div>
                </div>
                <StatusChip tom="neutro">Ninguém precisa</StatusChip>
              </div>
            );
          }
          return (
            <div key={et.id} className={e.linhaResumo}>
              <span className={mergeClasses(e.numero, e.numeroFeito)} aria-hidden="true"><Checkmark16Regular /></span>
              <div>
                <b>{rot.codigo} · {n} certificado{n > 1 ? 's' : ''}</b>
                <div className={e.mudo}>
                  {lista(nomesCurtos(participantesAtivos(et).map((p) => p.nome)))}. {n} assinatura{n > 1 ? 's' : ''} biométrica{n > 1 ? 's' : ''}.
                  {semCert.length > 0 ? ` Não assinou, sem certificado: ${lista(nomesCurtos(semCert.map((p) => p.nome)))}.` : ''}
                </div>
              </div>
              <StatusChip tom="ok">{n} assinaturas</StatusChip>
            </div>
          );
        })}
        {etapaOs && (
          <div className={e.linhaResumo}>
            <span className={mergeClasses(e.numero, e.numeroFeito)} aria-hidden="true"><Checkmark16Regular /></span>
            <div>
              <b>Ordem de Serviço (NR-01) · {osAssinadas.length} OS assinada{osAssinadas.length === 1 ? '' : 's'}</b>
              <div className={e.mudo}>
                {lista(nomesCurtos(osAssinadas.map((p) => p.nome)))}.
                {osPendentes.length > 0 ? ` ${lista(nomesCurtos(osPendentes.map((p) => p.nome)))} ficam com a OS pendente.` : ''} Registro isolado no perfil, sem certificado.
              </div>
            </div>
            <StatusChip tom="ok">{osAssinadas.length} assinaturas</StatusChip>
          </div>
        )}
      </Card>

      <Card
        titulo={<TituloSecao numero={<Camera16Regular />}>Fotos por etapa</TituloSecao>}
        subtitulo="Cada curso tem as próprias 3 fotos, em horários distintos, nunca reaproveitadas."
      >
        {etapasCurso.filter((et) => participantesAtivos(et).length > 0).map((et) => (
          <div key={et.id} className={e.linhaResumo}>
            <span className={e.numero} aria-hidden="true"><Camera16Regular /></span>
            <div>
              <b>{rotuloEtapa(et.tipo).codigo}</b>
              <div className={e.mudo}>{(horariosFotos[et.id] ?? []).join(' · ') || 'Horários das fotos registrados no certificado.'}</div>
            </div>
            <StatusChip tom="info">3 fotos exclusivas</StatusChip>
          </div>
        ))}
      </Card>

      <Card
        titulo={<TituloSecao numero="·">Selo no perfil de cada pessoa</TituloSecao>}
        subtitulo='O selo "Integração realizada" não é um certificado: é só o registro de que a integração foi feita.'
      >
        {integracao.participantes.map((p) => {
          const ok = osAssinadas.some((o) => o.trabalhadorId === p.trabalhadorId);
          return (
            <div key={p.trabalhadorId} className={e.linhaResumo}>
              <AvatarIniciais nome={p.nome} pequeno />
              <div>
                <b>{p.nome}</b> <span className={e.funcaoPessoa}>{p.funcao}</span>
                <div className={e.mudo}>{ok ? 'OS assinada. Perfil atualizado.' : 'OS pendente. O selo sai quando a OS for assinada, pelo perfil.'}</div>
              </div>
              {ok ? <StatusChip tom="ok">Integração realizada</StatusChip> : <StatusChip tom="atencao">Aguardando OS</StatusChip>}
            </div>
          );
        })}
      </Card>
    </>
  );
}
