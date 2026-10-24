import { useState } from 'react';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import {
  BotaoAcao,
  Button,
  Campo,
  FeedbackInline,
  Field,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  Select,
} from '@ui';
import type { Funcao, Obra } from '../../lib/api';
import type { ValorFormularioContrato } from './valorFormularioContrato';

// Contrato cadastrado à mão pelo técnico (18/10/2026) — mesmo formulário para criar (na ficha da
// empresa) e editar (no detalhe do contrato). A obra só é escolhida na criação: trocar a obra de um
// contrato com gente cadastrada deixaria as pessoas na obra antiga.

interface Props {
  valorInicial: ValorFormularioContrato;
  obras?: Obra[];
  funcoes: Funcao[];
  // funcaoId -> pessoas já cadastradas (só na edição): a quantidade não pode ficar abaixo disso.
  preenchidasPorFuncao?: Record<string, number>;
  erro: string | null;
  aoFecharErro: () => void;
  salvando: boolean;
  rotuloSalvar: string;
  aoSalvar: (valor: ValorFormularioContrato) => void;
  aoCancelar: () => void;
}

export function FormularioContrato({
  valorInicial,
  obras,
  funcoes,
  preenchidasPorFuncao = {},
  erro,
  aoFecharErro,
  salvando,
  rotuloSalvar,
  aoSalvar,
  aoCancelar,
}: Props) {
  const [valor, setValor] = useState(valorInicial);

  function alterarVaga(indice: number, campo: 'funcaoId' | 'quantidade', novo: string) {
    setValor({ ...valor, vagas: valor.vagas.map((v, i) => (i === indice ? { ...v, [campo]: novo } : v)) });
  }

  const funcoesUsadas = new Set(valor.vagas.map((v) => v.funcaoId).filter(Boolean));

  return (
    <>
      <FormSection titulo="Dados do contrato" numero={1} primeira>
        {erro && (
          <FeedbackInline tom="erro" aoFechar={aoFecharErro}>
            {erro}
          </FeedbackInline>
        )}
        <FormGrid>
          {obras && (
            <Campo span={6}>
              <Field label="Obra" required>
                <Select style={{ width: '100%' }} value={valor.obraId} onChange={(_, d) => setValor({ ...valor, obraId: d.value })}>
                  <option value="">Selecione</option>
                  {obras.map((obra) => (
                    <option key={obra.id} value={obra.id}>
                      {obra.codigo} — {obra.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
          )}
          <Campo span={obras ? 6 : 4}>
            <Field label="Nº do contrato" required>
              <Input
                value={valor.numeroContrato}
                maxLength={50}
                onChange={(_, d) => setValor({ ...valor, numeroContrato: d.value })}
              />
            </Field>
          </Campo>
          <Campo span={obras ? 6 : 4}>
            <Field label="Início da vigência" required>
              <Input
                type="date"
                value={valor.dataInicioVigencia}
                onChange={(_, d) => setValor({ ...valor, dataInicioVigencia: d.value })}
              />
            </Field>
          </Campo>
          <Campo span={obras ? 6 : 4}>
            <Field label="Fim da vigência" required>
              <Input
                type="date"
                value={valor.dataFimVigencia}
                min={valor.dataInicioVigencia || undefined}
                onChange={(_, d) => setValor({ ...valor, dataFimVigencia: d.value })}
              />
            </Field>
          </Campo>
        </FormGrid>
      </FormSection>

      <FormSection titulo="Vagas por função" numero={2}>
        <FormGrid>
          {valor.vagas.map((vaga, indice) => {
            const preenchidas = preenchidasPorFuncao[vaga.funcaoId] ?? 0;
            return (
              <Campo span={12} key={indice}>
                <div style={{ display: 'flex', gap: 12, alignItems: 'flex-end', flexWrap: 'wrap' }}>
                  <div style={{ flex: '1 1 260px', minWidth: 0 }}>
                    <Field label="Função" required>
                      <Select style={{ width: '100%' }}
                        value={vaga.funcaoId}
                        disabled={preenchidas > 0}
                        onChange={(_, d) => alterarVaga(indice, 'funcaoId', d.value)}
                      >
                        <option value="">Selecione</option>
                        {funcoes
                          .filter((f) => f.id === vaga.funcaoId || !funcoesUsadas.has(f.id))
                          .map((f) => (
                            <option key={f.id} value={f.id}>
                              {f.nome}
                            </option>
                          ))}
                      </Select>
                    </Field>
                  </div>
                  <div style={{ flex: '0 0 140px', minWidth: 0 }}>
                    <Field
                      label="Quantidade"
                      required
                      hint={preenchidas > 0 ? `${preenchidas} já cadastrada(s)` : undefined}
                    >
                      <Input
                        type="number"
                        min={Math.max(1, preenchidas)}
                        value={vaga.quantidade}
                        onChange={(_, d) => alterarVaga(indice, 'quantidade', d.value)}
                      />
                    </Field>
                  </div>
                  <BotaoAcao
                    tom="excluir"
                    icon={<Delete24Regular />}
                    disabled={valor.vagas.length === 1 || preenchidas > 0}
                    onClick={() => setValor({ ...valor, vagas: valor.vagas.filter((_, i) => i !== indice) })}
                    aria-label="Remover vaga"
                  />
                </div>
              </Campo>
            );
          })}
        </FormGrid>
        <div>
          <Button
            appearance="subtle"
            icon={<Add24Regular />}
            disabled={funcoesUsadas.size >= funcoes.length}
            onClick={() => setValor({ ...valor, vagas: [...valor.vagas, { funcaoId: '', quantidade: '1' }] })}
          >
            Adicionar função
          </Button>
        </div>
        <FormRodape info="As pessoas da empresa são cadastradas depois, em cada vaga do contrato.">
          <Button onClick={aoCancelar}>Cancelar</Button>
          <Button appearance="primary" onClick={() => aoSalvar(valor)} disabled={salvando}>
            {rotuloSalvar}
          </Button>
        </FormRodape>
      </FormSection>
    </>
  );
}
