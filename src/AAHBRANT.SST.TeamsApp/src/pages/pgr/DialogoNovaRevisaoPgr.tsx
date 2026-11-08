import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button,
  CampoData,
  Checkbox,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  FeedbackInline,
  Field,
  Text,
  Textarea,
} from '@ui';
import { api } from '../../lib/api';

// Nova revisão do PGR (07/11/2026, mockup pgr-nova-revisao_2026-11-07_v1): pergunta o motivo.
// - Mudança na obra: abre a geração já com o que foi aprovado ("Mantido") para acrescentar itens.
// - Revisão periódica: nada muda no inventário; o engenheiro confirma que reavaliou os riscos (NR-01) e a geração vai
//   direto ao passo de aprovação e assinatura.
type Tipo = 'Mudanca' | 'Periodica';

function somarAnos(iso: string, anos: number) {
  const d = new Date(iso + 'T00:00:00');
  d.setFullYear(d.getFullYear() + anos);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

export function DialogoNovaRevisaoPgr({
  aberto,
  obraId,
  obraNome,
  rotuloAtual,
  aoFechar,
}: {
  aberto: boolean;
  obraId: string;
  obraNome: string;
  rotuloAtual: string;
  aoFechar: () => void;
}) {
  const navigate = useNavigate();
  // Data local (toISOString daria o dia seguinte à noite, por causa do fuso).
  const agora = new Date();
  const hoje = `${agora.getFullYear()}-${String(agora.getMonth() + 1).padStart(2, '0')}-${String(agora.getDate()).padStart(2, '0')}`;
  const [tipo, setTipo] = useState<Tipo>('Mudanca');
  const [elaboracao, setElaboracao] = useState(hoje);
  // Próxima revisão é sempre 1 ano após a elaboração (regra da empresa, 07/11/2026): calculada, não editável.
  const proxima = elaboracao ? somarAnos(elaboracao, 1) : '';
  const [observacao, setObservacao] = useState('');
  const [confirmou, setConfirmou] = useState(false);
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  async function continuar() {
    try {
      setEnviando(true);
      setErro(null);
      const { id } = await api.geracoesPgrEap.criarRevisao({
        obraId,
        tipo,
        dataElaboracao: elaboracao,
        dataProximaRevisao: proxima || null,
        dataTermino: proxima || null,
        observacao: observacao.trim() || null,
        reavaliacaoConfirmada: tipo === 'Periodica' && confirmou,
      });
      aoFechar();
      navigate(`/prevencao/pgr/gerar-eap/${id}`);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível abrir a revisão.');
    } finally {
      setEnviando(false);
    }
  }

  const opcao = (valor: Tipo, titulo: string, texto: string) => {
    const sel = tipo === valor;
    return (
      <button
        type="button"
        role="radio"
        aria-checked={sel}
        onClick={() => setTipo(valor)}
        style={{
          display: 'flex',
          gap: 12,
          textAlign: 'left',
          width: '100%',
          padding: 14,
          borderRadius: 10,
          cursor: 'pointer',
          font: 'inherit',
          border: `2px solid ${sel ? '#670000' : '#e5e7eb'}`,
          background: sel ? '#fdf7f7' : '#fff',
        }}
      >
        <span
          style={{
            width: 16,
            height: 16,
            borderRadius: '50%',
            flex: 'none',
            marginTop: 2,
            border: sel ? '5px solid #670000' : '2px solid #9ca3af',
          }}
        />
        <span>
          <b style={{ display: 'block', fontSize: 14, marginBottom: 2 }}>{titulo}</b>
          <span style={{ fontSize: 12, color: '#6b7280' }}>{texto}</span>
        </span>
      </button>
    );
  };

  const podeContinuar = !!elaboracao && (tipo === 'Mudanca' || confirmou);

  return (
    <Dialog open={aberto} onOpenChange={(_, d) => !d.open && aoFechar()}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Nova revisão do PGR · {obraNome}</DialogTitle>
          <DialogContent>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              <Text size={200}>{rotuloAtual}</Text>
              <div role="radiogroup" aria-label="Motivo da revisão" style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                {opcao(
                  'Mudanca',
                  'Mudança na obra',
                  'Novas atividades, novo GHE ou nova função. Abre o PGR atual para acrescentar o que mudou — o que já existe continua como está.',
                )}
                {opcao(
                  'Periodica',
                  'Revisão periódica (validade)',
                  'Nada mudou na obra. O engenheiro confirma que reavaliou os riscos e o PGR ganha nova vigência.',
                )}
              </div>
              <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
                <Field label="Nova data de elaboração" required>
                  <CampoData value={elaboracao} onChange={(_, d) => setElaboracao(d.value)} />
                </Field>
                <Field label="Próxima revisão" hint="Sempre 1 ano após a elaboração">
                  <CampoData value={proxima} disabled onChange={() => undefined} />
                </Field>
              </div>
              <Field label="Observação (opcional)">
                <Textarea
                  value={observacao}
                  maxLength={500}
                  placeholder={tipo === 'Periodica' ? 'Ex.: obra no mesmo estágio, sem novas frentes.' : 'Ex.: início da impermeabilização.'}
                  onChange={(_, d) => setObservacao(d.value)}
                />
              </Field>
              {tipo === 'Periodica' && (
                <div style={{ background: '#fffbeb', border: '1px solid #fde68a', borderRadius: 8, padding: 10 }}>
                  <Checkbox
                    checked={confirmou}
                    onChange={(_, d) => setConfirmou(!!d.checked)}
                    label="Reavaliei o inventário de riscos e não há alterações."
                  />
                  <Text size={200} style={{ display: 'block', marginLeft: 30 }}>
                    Exigência da NR-01 para a revisão periódica. Fica registrado com seu nome e data.
                  </Text>
                </div>
              )}
              {erro && <FeedbackInline tom="erro">{erro}</FeedbackInline>}
            </div>
          </DialogContent>
          <DialogActions>
            <Button onClick={aoFechar}>Cancelar</Button>
            <Button appearance="primary" disabled={!podeContinuar || enviando} onClick={() => void continuar()}>
              {enviando ? 'Abrindo…' : tipo === 'Periodica' ? 'Seguir para aprovação' : 'Continuar'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
