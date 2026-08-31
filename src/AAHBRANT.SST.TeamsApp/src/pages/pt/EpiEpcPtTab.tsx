import { useEffect, useState } from 'react';
import { Button, Checkbox, Field, Input, Text, Textarea } from '@fluentui/react-components';
import { Save24Regular } from '@fluentui/react-icons';
import {
  api,
  itemEpcPtLabel,
  itemEpiPtLabel,
  type EpiPtInput,
  type PermissaoTrabalho,
  type PermissaoTrabalhoEpc,
  type PermissaoTrabalhoEpi,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

// §5 do formulário — EPIs/EPCs aplicáveis à atividade; algumas opções de EPI têm complemento de
// texto livre embutido no próprio formulário (ver disclosure em ItemEpiPt no backend).
export function EpiEpcPtTab({
  permissaoTrabalhoId,
  pt,
  episAtuais,
  epcsAtuais,
  aoAtualizar,
}: {
  permissaoTrabalhoId: string;
  pt: PermissaoTrabalho;
  episAtuais: PermissaoTrabalhoEpi[];
  epcsAtuais: PermissaoTrabalhoEpc[];
  aoAtualizar: () => Promise<void>;
}) {
  const estilos = usePageStyles();
  const [episSelecionados, setEpisSelecionados] = useState<Map<number, string>>(new Map());
  const [epcsSelecionados, setEpcsSelecionados] = useState<Set<number>>(new Set());
  const [outrosEpis, setOutrosEpis] = useState('');
  const [outrosEpcs, setOutrosEpcs] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);

  useEffect(() => {
    setEpisSelecionados(new Map(episAtuais.map((item) => [item.item, item.complemento ?? ''])));
    setEpcsSelecionados(new Set(epcsAtuais.map((item) => item.item)));
    setOutrosEpis(pt.outrosEpis ?? '');
    setOutrosEpcs(pt.outrosEpcs ?? '');
  }, [episAtuais, epcsAtuais, pt.outrosEpis, pt.outrosEpcs]);

  function alternarEpi(item: number, marcado: boolean) {
    setEpisSelecionados((atual) => {
      const novo = new Map(atual);
      if (marcado) novo.set(item, novo.get(item) ?? '');
      else novo.delete(item);
      return novo;
    });
  }

  function alternarEpc(item: number, marcado: boolean) {
    setEpcsSelecionados((atual) => {
      const novo = new Set(atual);
      if (marcado) novo.add(item);
      else novo.delete(item);
      return novo;
    });
  }

  async function salvar() {
    try {
      setSalvando(true);
      setErro(null);
      const itensEpi: EpiPtInput[] = [...episSelecionados.entries()].map(([item, complemento]) => ({
        item,
        complemento: complemento || null,
      }));
      await Promise.all([
        api.permissoesTrabalho.definirEpis(permissaoTrabalhoId, itensEpi, outrosEpis || null),
        api.permissoesTrabalho.definirEpcs(permissaoTrabalhoId, [...epcsSelecionados], outrosEpcs || null),
      ]);
      await aoAtualizar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar EPIs/EPCs.');
    } finally {
      setSalvando(false);
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">EPIs / EPCs aplicáveis</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div style={{ display: 'flex', gap: 32, flexWrap: 'wrap' }}>
        <Field label="EPIs" style={{ flex: 1, minWidth: 280 }}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            {Object.entries(itemEpiPtLabel).map(([valor, rotulo]) => (
              <div key={valor} style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <Checkbox
                  label={rotulo}
                  checked={episSelecionados.has(Number(valor))}
                  onChange={(_, d) => alternarEpi(Number(valor), !!d.checked)}
                />
                {episSelecionados.has(Number(valor)) && (
                  <Input
                    placeholder="Complemento (opcional)"
                    size="small"
                    value={episSelecionados.get(Number(valor)) ?? ''}
                    onChange={(_, d) =>
                      setEpisSelecionados((atual) => new Map(atual).set(Number(valor), d.value))
                    }
                  />
                )}
              </div>
            ))}
          </div>
          <Textarea
            style={{ marginTop: 8 }}
            placeholder="Outros EPIs"
            value={outrosEpis}
            onChange={(_, d) => setOutrosEpis(d.value)}
          />
        </Field>

        <Field label="EPCs" style={{ flex: 1, minWidth: 280 }}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            {Object.entries(itemEpcPtLabel).map(([valor, rotulo]) => (
              <Checkbox
                key={valor}
                label={rotulo}
                checked={epcsSelecionados.has(Number(valor))}
                onChange={(_, d) => alternarEpc(Number(valor), !!d.checked)}
              />
            ))}
          </div>
          <Textarea
            style={{ marginTop: 8 }}
            placeholder="Outros EPCs"
            value={outrosEpcs}
            onChange={(_, d) => setOutrosEpcs(d.value)}
          />
        </Field>
      </div>

      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Save24Regular />} onClick={salvar} disabled={salvando}>
          Salvar EPIs/EPCs
        </Button>
      </div>
    </div>
  );
}
