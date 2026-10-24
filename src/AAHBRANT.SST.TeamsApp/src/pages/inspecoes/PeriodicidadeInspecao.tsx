import { Campo, Field, Input, Legenda, Select, StatusChip, type Tom } from '@ui';
import type { Usuario } from '../../lib/api';

// Inspeção periódica por item (24/10/2026): "a cada N dias desde a última (ou primeira) inspeção", com
// um responsável que recebe o aviso do Motor de Alertas. Compartilhado por Veículos e itens de tema.
export const PERIODICIDADE_PADRAO_VEICULO = '30';
const MINIMO_DIAS = 1;
const MAXIMO_DIAS = 365;

export interface PeriodicidadeEmEdicao {
  dias: string; // vazio = sem periodicidade
  responsavelId: string;
}

// Mensagem de pendência ou null quando está válido; converte os dias para número em payloadPeriodicidade.
export function pendenciaPeriodicidade(p: PeriodicidadeEmEdicao): string | null {
  if (!p.dias.trim()) return null;
  const dias = Number(p.dias);
  if (!Number.isInteger(dias) || dias < MINIMO_DIAS || dias > MAXIMO_DIAS)
    return `A periodicidade deve ficar entre ${MINIMO_DIAS} e ${MAXIMO_DIAS} dias.`;
  if (!p.responsavelId) return 'Designe quem será avisado da inspeção periódica.';
  return null;
}

export function payloadPeriodicidade(p: PeriodicidadeEmEdicao) {
  const dias = p.dias.trim() ? Number(p.dias) : null;
  return { periodicidadeInspecaoDias: dias, responsavelInspecaoUsuarioId: dias ? p.responsavelId : null };
}

export function CamposPeriodicidadeInspecao({
  valor,
  aoMudar,
  usuarios,
  desabilitado = false,
}: {
  valor: PeriodicidadeEmEdicao;
  aoMudar: (valor: PeriodicidadeEmEdicao) => void;
  usuarios: Usuario[];
  desabilitado?: boolean;
}) {
  return (
    <>
      <Campo span={4}>
        <Field label="Inspecionar a cada (dias)" hint="Conta da última inspeção concluída — ou do cadastro, se nunca foi inspecionado. Vazio = sem aviso.">
          <Input
            type="number"
            min={MINIMO_DIAS}
            max={MAXIMO_DIAS}
            value={valor.dias}
            disabled={desabilitado}
            onChange={(_, d) => aoMudar({ ...valor, dias: d.value })}
            placeholder="Ex.: 30"
          />
        </Field>
      </Campo>
      <Campo span={8}>
        <Field label="Responsável pela inspeção" required={!!valor.dias.trim()} hint="Recebe o aviso quando a inspeção estiver perto de vencer ou vencida.">
          <Select value={valor.responsavelId} disabled={desabilitado} onChange={(_, d) => aoMudar({ ...valor, responsavelId: d.value })}>
            <option value="">Selecione</option>
            {usuarios.map((u) => (
              <option key={u.id} value={u.id}>
                {u.nome}
              </option>
            ))}
          </Select>
        </Field>
      </Campo>
    </>
  );
}

const MS_DIA = 24 * 60 * 60 * 1000;

// Selo "Vence em dd/MM (X dias)" / "Vencida há X dias" a partir de ProximaInspecaoEm.
export function SeloProximaInspecao({
  proximaEm,
  dias,
  responsavelNome,
}: {
  proximaEm?: string | null;
  dias?: number | null;
  responsavelNome?: string | null;
}) {
  if (!proximaEm || !dias) return null;
  const vencimento = new Date(proximaEm);
  const hoje = new Date();
  const restantes = Math.round((Date.UTC(vencimento.getFullYear(), vencimento.getMonth(), vencimento.getDate())
    - Date.UTC(hoje.getFullYear(), hoje.getMonth(), hoje.getDate())) / MS_DIA);
  const data = vencimento.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' });
  const tom: Tom = restantes < 0 ? 'alerta' : restantes <= 7 ? 'atencao' : 'info';
  const texto =
    restantes < 0
      ? `Inspeção vencida há ${-restantes} ${restantes === -1 ? 'dia' : 'dias'} (${data})`
      : restantes === 0
        ? 'Inspeção vence hoje'
        : `Próxima inspeção ${data} · em ${restantes} ${restantes === 1 ? 'dia' : 'dias'}`;
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <span>
        <StatusChip tom={tom}>{texto}</StatusChip>
      </span>
      <Legenda>
        A cada {dias} {dias === 1 ? 'dia' : 'dias'}
        {responsavelNome ? ` · avisa ${responsavelNome}` : ''}
      </Legenda>
    </div>
  );
}
