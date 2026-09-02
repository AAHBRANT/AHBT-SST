import { useEffect, useState } from 'react';
import { Button, Checkbox, Field, Input, Text } from '@fluentui/react-components';
import { Save24Regular } from '@fluentui/react-icons';
import {
  TipoTrabalhoEspecialPt,
  api,
  tipoTrabalhoEspecialPtLabel,
  type PermissaoTrabalhoTipoTrabalho,
  type TipoTrabalhoPtInput,
} from '../../lib/api';
import { usePageStyles, useCheckboxChipStyles } from '../pageStyles';

// §3 do formulário — 12 opções fixas, multi-select: só os tipos marcados viram linha (ver
// disclosure em DefinirTiposTrabalhoPtCommand.cs). "Outro" carrega texto livre complementar.
export function TiposTrabalhoPtTab({
  permissaoTrabalhoId,
  itens,
  aoAtualizar,
}: {
  permissaoTrabalhoId: string;
  itens: PermissaoTrabalhoTipoTrabalho[];
  aoAtualizar: () => Promise<void>;
}) {
  const estilos = usePageStyles();
  const estilosChip = useCheckboxChipStyles();
  const [selecionados, setSelecionados] = useState<Map<number, string>>(new Map());
  const [erro, setErro] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);

  useEffect(() => {
    setSelecionados(new Map(itens.map((item) => [item.tipo, item.descricaoOutro ?? ''])));
  }, [itens]);

  function alternar(tipo: number, marcado: boolean) {
    setSelecionados((atual) => {
      const novo = new Map(atual);
      if (marcado) novo.set(tipo, novo.get(tipo) ?? '');
      else novo.delete(tipo);
      return novo;
    });
  }

  function atualizarDescricaoOutro(descricao: string) {
    setSelecionados((atual) => new Map(atual).set(TipoTrabalhoEspecialPt.Outro, descricao));
  }

  async function salvar() {
    try {
      setSalvando(true);
      setErro(null);
      const tipos: TipoTrabalhoPtInput[] = [...selecionados.entries()].map(([tipo, descricaoOutro]) => ({
        tipo,
        descricaoOutro: tipo === TipoTrabalhoEspecialPt.Outro ? descricaoOutro || null : null,
      }));
      await api.permissoesTrabalho.definirTiposTrabalho(permissaoTrabalhoId, tipos);
      await aoAtualizar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar tipos de trabalho.');
    } finally {
      setSalvando(false);
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Tipos de trabalho especiais / permissões específicas</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, marginBottom: 16 }}>
        {Object.entries(tipoTrabalhoEspecialPtLabel).map(([valor, rotulo]) => (
          <Checkbox
            key={valor}
            className={estilosChip.chip}
            label={rotulo}
            checked={selecionados.has(Number(valor))}
            onChange={(_, d) => alternar(Number(valor), !!d.checked)}
          />
        ))}
      </div>

      {selecionados.has(TipoTrabalhoEspecialPt.Outro) && (
        <Field label="Descreva o tipo de trabalho (Outro)" style={{ marginBottom: 16, maxWidth: 400 }}>
          <Input
            value={selecionados.get(TipoTrabalhoEspecialPt.Outro) ?? ''}
            onChange={(_, d) => atualizarDescricaoOutro(d.value)}
          />
        </Field>
      )}

      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Save24Regular />} onClick={salvar} disabled={salvando}>
          Salvar tipos de trabalho
        </Button>
      </div>
    </div>
  );
}
