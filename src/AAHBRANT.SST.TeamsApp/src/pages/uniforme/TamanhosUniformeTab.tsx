import { useEffect, useState } from 'react';
import { Button, Field, Input, Select, Table, TableBody, TableCell, TableHeader, TableHeaderCell, TableRow, Text } from '@fluentui/react-components';
import { Save24Regular } from '@fluentui/react-icons';
import { api, type CatalogoUniforme, type ItemTamanhoUniforme, type Trabalhador } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useSucessoToast } from '../../hooks/useSucessoToast';

// Tamanho de uniforme por trabalhador — nova tela (não tem equivalente no EPI). Busca um
// trabalhador, lista todas as peças do catálogo e permite registrar o tamanho de cada uma; peças
// deixadas em branco não geram vínculo (o trabalhador simplesmente não tem tamanho cadastrado
// para elas ainda, e a Entrega bloqueia até que seja cadastrado).
export function TamanhosUniformeTab() {
  const estilos = usePageStyles();
  const sucessoToast = useSucessoToast();
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [itensCatalogo, setItensCatalogo] = useState<CatalogoUniforme[]>([]);
  const [trabalhadorId, setTrabalhadorId] = useState('');
  const [tamanhos, setTamanhos] = useState<Record<string, string>>({});
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  useEffect(() => {
    (async () => {
      try {
        const [listaTrabalhadores, listaItens] = await Promise.all([api.trabalhadores.listar(), api.catalogosUniforme.listar()]);
        setTrabalhadores(listaTrabalhadores);
        setItensCatalogo(listaItens);
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar trabalhadores e catálogo de uniforme.');
      }
    })();
  }, []);

  useEffect(() => {
    if (!trabalhadorId) {
      setTamanhos({});
      return;
    }
    (async () => {
      try {
        setErro(null);
        const lista = await api.trabalhadores.listarTamanhosUniforme(trabalhadorId);
        const mapa: Record<string, string> = {};
        for (const item of lista) mapa[item.catalogoUniformeId] = item.tamanho;
        setTamanhos(mapa);
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar tamanhos do trabalhador.');
      }
    })();
  }, [trabalhadorId]);

  async function salvar() {
    if (!trabalhadorId) return;
    try {
      setCarregando(true);
      setErro(null);
      const itens: ItemTamanhoUniforme[] = Object.entries(tamanhos)
        .filter(([, tamanho]) => tamanho.trim() !== '')
        .map(([catalogoUniformeId, tamanho]) => ({ catalogoUniformeId, tamanho: tamanho.trim() }));
      await api.trabalhadores.definirTamanhosUniforme(trabalhadorId, itens);
      sucessoToast('Tamanhos de uniforme salvos com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar tamanhos de uniforme.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Tamanhos de uniforme por trabalhador</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <Field label="Trabalhador">
        <Select value={trabalhadorId} onChange={(_, d) => setTrabalhadorId(d.value)}>
          <option value="">Selecione</option>
          {trabalhadores.map((t) => (
            <option key={t.id} value={t.id}>
              {t.nome} ({t.matricula})
            </option>
          ))}
        </Select>
      </Field>

      {trabalhadorId && (
        <>
          <Table noNativeElements style={{ marginTop: 12 }}>
            <TableHeader>
              <TableRow>
                <TableHeaderCell>Peça</TableHeaderCell>
                <TableHeaderCell>Tamanho</TableHeaderCell>
              </TableRow>
            </TableHeader>
            <TableBody>
              {itensCatalogo.map((item) => (
                <TableRow key={item.id}>
                  <TableCell>{item.nome}</TableCell>
                  <TableCell>
                    <Input
                      value={tamanhos[item.id] ?? ''}
                      onChange={(_, d) => setTamanhos({ ...tamanhos, [item.id]: d.value })}
                      placeholder="ex.: M, 42..."
                      style={{ width: 100 }}
                    />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <div className={estilos.formActions}>
            <Button appearance="primary" icon={<Save24Regular />} onClick={salvar} disabled={carregando}>
              Salvar tamanhos
            </Button>
          </div>
        </>
      )}
    </div>
  );
}
