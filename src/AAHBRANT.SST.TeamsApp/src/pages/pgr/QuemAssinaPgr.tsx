import { useEffect, useState } from 'react';
import { Card, Checkbox, Field, FeedbackInline, Input, Select, Text } from '@ui';
import { api, type AssinanteDisponivel, type AssinanteGeracaoPgr, type AssinantesGeracaoPgr, type EstruturaGeracaoPgr } from '../../lib/api';

// Quem assina o PGR: o técnico de segurança da obra (assina ao enviar para aprovação) e o engenheiro (assina ao aprovar).
// A pessoa é escolhida aqui, o registro profissional é digitado em cada PGR e "assinatura à mão" deixa a linha em branco no PDF.
export function QuemAssinaPgr({ geracaoId, e, editavel, alterar }: {
  geracaoId: string;
  e: EstruturaGeracaoPgr;
  editavel: boolean;
  alterar: (f: (x: EstruturaGeracaoPgr) => void) => void;
}) {
  const [opcoes, setOpcoes] = useState<AssinantesGeracaoPgr | null>(null);
  useEffect(() => {
    api.geracoesPgrEap.assinantes(geracaoId).then(setOpcoes).catch(() => setOpcoes({ tecnicos: [], engenheiros: [] }));
  }, [geracaoId]);

  const comp = e.complementares;
  if (!comp) return null;

  return (
    <Card titulo="Quem assina">
      <Text size={200} style={{ display: 'block', marginBottom: 8 }}>
        O técnico assina ao enviar para aprovação e o engenheiro ao aprovar, cada um com o próprio acesso ao sistema. O carimbo da assinatura,
        com nome, registro, data e hora, aparece no PDF final junto com um QR para conferir a autenticidade. Quem assina à mão fica com a linha em branco.
      </Text>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: 16 }}>
        <LinhaAssinante titulo="Técnico de Segurança da obra" rotuloRegistro="Registro profissional (ex.: CFT)" lista={opcoes?.tecnicos}
          valor={comp.tecnico} editavel={editavel} alterar={(v) => alterar((x) => { x.complementares!.tecnico = v; })} />
        <LinhaAssinante titulo="Engenheiro de Segurança do Trabalho" rotuloRegistro="Registro profissional (ex.: CREA)" lista={opcoes?.engenheiros}
          valor={comp.engenheiro} editavel={editavel} alterar={(v) => alterar((x) => { x.complementares!.engenheiro = v; })} />
      </div>
    </Card>
  );
}

function LinhaAssinante({ titulo, rotuloRegistro, lista, valor, editavel, alterar }: {
  titulo: string;
  rotuloRegistro: string;
  lista?: AssinanteDisponivel[];
  valor?: AssinanteGeracaoPgr | null;
  editavel: boolean;
  alterar: (v: AssinanteGeracaoPgr | null) => void;
}) {
  const aMao = !!valor?.aMao;
  const escolhido = lista?.find((u) => u.usuarioId === valor?.usuarioId);
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      <Text weight="semibold">{titulo}</Text>
      <Field label="Quem assina">
        <Select
          value={aMao ? '' : valor?.usuarioId ?? ''}
          disabled={!editavel || aMao}
          onChange={(_, d) => {
            const u = lista?.find((x) => x.usuarioId === d.value);
            alterar(u ? { usuarioId: u.usuarioId, nome: u.nome, registro: valor?.registro ?? null, aMao: false } : null);
          }}
        >
          <option value="">Escolha a pessoa…</option>
          {(lista ?? []).map((u) => <option key={u.usuarioId} value={u.usuarioId}>{u.nome}</option>)}
        </Select>
      </Field>
      {!aMao && escolhido && !escolhido.vinculado && (
        <FeedbackInline tom="aviso">
          O usuário de {escolhido.nome} não está ligado a um cadastro de trabalhador. Peça a um administrador para vincular antes de assinar.
        </FeedbackInline>
      )}
      {!aMao && (
        <Field label={rotuloRegistro}>
          <Input
            value={valor?.registro ?? ''}
            disabled={!editavel || !valor}
            maxLength={60}
            onChange={(_, d) => valor && alterar({ ...valor, registro: d.value })}
          />
        </Field>
      )}
      <Checkbox
        label="Assinatura à mão (fora do sistema)"
        checked={aMao}
        disabled={!editavel}
        onChange={(_, d) => alterar(d.checked ? { aMao: true, usuarioId: null, nome: null, registro: null } : null)}
      />
    </div>
  );
}
