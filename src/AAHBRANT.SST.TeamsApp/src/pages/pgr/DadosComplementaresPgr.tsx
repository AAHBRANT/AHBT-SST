import { Card, Field, Input, Select } from '@ui';
import type { EstruturaGeracaoPgr } from '../../lib/api';

// Dados da obra que o PGR exige (24/10/2026). O técnico preenche uma vez; as próximas gerações da mesma obra abrem já
// preenchidas. A resposta a emergências (contatos, rotas, cenários) saiu do PGR em 30/10/2026: fica no PAE da obra.
export function DadosComplementaresPgr({ e, editavel, alterar }: {
  e: EstruturaGeracaoPgr;
  editavel: boolean;
  alterar: (f: (x: EstruturaGeracaoPgr) => void) => void;
}) {
  const comp = e.complementares;

  return (
    <Card titulo="Dados da obra para o PGR">
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 8 }}>
        <Field label="CNAE (número e descrição)">
          <Input value={comp.cnae ?? ''} disabled={!editavel} placeholder="Ex.: 4211-1/01 — Construção de rodovias"
            onChange={(_, d) => alterar((x) => { x.complementares.cnae = d.value; })} />
        </Field>
        <Field label="Grau de risco (NR-04)">
          <Select value={comp.grauRisco != null ? String(comp.grauRisco) : ''} disabled={!editavel}
            onChange={(_, d) => alterar((x) => { x.complementares.grauRisco = d.value ? Number(d.value) : null; })}>
            <option value="">Selecione…</option>
            {[1, 2, 3, 4].map((n) => <option key={n} value={n}>{n}</option>)}
          </Select>
        </Field>
        <Field label="CEP">
          <Input value={comp.cep ?? ''} disabled={!editavel} maxLength={9}
            onChange={(_, d) => alterar((x) => { x.complementares.cep = d.value; })} />
        </Field>
        <Field label="Registro profissional (CREA) do responsável técnico">
          <Input value={comp.responsavelTecnicoRegistro ?? ''} disabled={!editavel}
            onChange={(_, d) => alterar((x) => { x.complementares.responsavelTecnicoRegistro = d.value; })} />
        </Field>
        <Field label="Responsável pela implantação do PGR">
          <Input value={comp.responsavelImplantacaoNome ?? ''} disabled={!editavel}
            onChange={(_, d) => alterar((x) => { x.complementares.responsavelImplantacaoNome = d.value; })} />
        </Field>
        <Field label="Cargo do responsável pela implantação">
          <Input value={comp.responsavelImplantacaoCargo ?? ''} disabled={!editavel}
            onChange={(_, d) => alterar((x) => { x.complementares.responsavelImplantacaoCargo = d.value; })} />
        </Field>
      </div>
    </Card>
  );
}
