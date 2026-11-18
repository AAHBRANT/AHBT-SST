import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Button, Campo, FeedbackInline, Field, FormGrid, Input, useTipografia } from '@ui';
import { Key20Regular } from '@fluentui/react-icons';
import { api, type EquipamentoCamera } from '../../lib/api';
import { mergeClasses, useEstilosCameraIp } from './estilosCameraIp';

const TAMANHO_MAXIMO = 100;

export interface PainelSenhaEquipamentoProps {
  equipamento: Pick<EquipamentoCamera, 'id' | 'nome' | 'senhaDefinida'>;
  aoSalvar: () => void;
  aoCancelar: () => void;
}

// Define ou troca a senha do equipamento. A senha só é gravada: o servidor nunca a devolve e a tela não a guarda
// depois de salvar (o estado local é limpo ao concluir).
export function PainelSenhaEquipamento({ equipamento, aoSalvar, aoCancelar }: PainelSenhaEquipamentoProps) {
  const e = useEstilosCameraIp();
  const tipo = useTipografia();
  const queryClient = useQueryClient();
  const [senha, setSenha] = useState('');
  const [repetir, setRepetir] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);

  const troca = equipamento.senhaDefinida;

  async function salvar() {
    if (!senha) return setErro(troca ? 'Informe a nova senha.' : 'Informe a senha.');
    if (senha.length > TAMANHO_MAXIMO) return setErro(`A senha pode ter no máximo ${TAMANHO_MAXIMO} caracteres.`);
    if (senha !== repetir) return setErro('As duas senhas precisam ser iguais.');
    try {
      setSalvando(true);
      setErro(null);
      await api.cameras.definirSenhaEquipamento(equipamento.id, senha);
      setSenha('');
      setRepetir('');
      await queryClient.invalidateQueries({ queryKey: ['cameras', 'equipamentos'] });
      await queryClient.invalidateQueries({ queryKey: ['cameras', 'lista'] });
      aoSalvar();
    } catch (ex) {
      setErro(ex instanceof Error ? ex.message : 'Não foi possível salvar a senha.');
    } finally {
      setSalvando(false);
    }
  }

  return (
    <div className={e.subPainel}>
      <div className={e.subPainelTitulo}>
        <Key20Regular aria-hidden="true" />
        {troca ? `Trocar a senha de "${equipamento.nome}"` : `Definir a senha de "${equipamento.nome}"`}
      </div>
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      <FormGrid>
        <Campo span={6}>
          <Field label={troca ? 'Nova senha' : 'Senha'} required dica="Digite a senha da câmera. Ela é guardada protegida.">
            <Input
              type="password"
              value={senha}
              autoComplete="new-password"
              placeholder={troca ? 'Digite a nova senha' : 'Digite a senha da câmera'}
              onChange={(_, d) => setSenha(d.value)}
            />
          </Field>
        </Campo>
        <Campo span={6}>
          <Field label={troca ? 'Repita a nova senha' : 'Repita a senha'} required dica="Digite a mesma senha de novo para confirmar.">
            <Input
              type="password"
              value={repetir}
              autoComplete="new-password"
              placeholder="Digite de novo"
              onChange={(_, d) => setRepetir(d.value)}
            />
          </Field>
        </Campo>
      </FormGrid>
      <div className={mergeClasses(tipo.legenda, e.dica)} style={{ marginTop: 0, marginBottom: 12 }}>
        Depois de salva, a senha não aparece mais em nenhuma tela. O agente recebe a senha em poucos minutos; a imagem
        continua chegando.
      </div>
      <div className={e.acoesLinha}>
        <Button appearance="primary" className={e.botao} onClick={() => void salvar()} disabled={salvando}>
          {troca ? 'Salvar nova senha' : 'Salvar senha'}
        </Button>
        <Button className={e.botao} onClick={aoCancelar} disabled={salvando}>
          Cancelar
        </Button>
      </div>
    </div>
  );
}
