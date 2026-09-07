import { useEffect, useState } from 'react';
import { Button, Input, Table, TableBody, TableCell, TableHeader, TableHeaderCell, TableRow, Text } from '@fluentui/react-components';
import { Save24Regular } from '@fluentui/react-icons';
import { api, type CatalogoUniforme, type ItemTamanhoUniforme } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useSucessoToast } from '../../hooks/useSucessoToast';

// Tamanho de uniforme por trabalhador — seção do perfil (docs/superpowers/specs/2026-09-07-modulo-
// uniforme-design.md: "nova seção na tela de perfil dele, ao lado das demais seções de dados
// cadastrais"). Substitui a aba global TamanhosUniformeTab.tsx (removida na revisão final do
// branch, que apontou o desvio da spec) — mesma API, sem o seletor de trabalhador, que aqui vem da
// própria página de perfil.
export function TamanhosUniformeSecao({ trabalhadorId }: { trabalhadorId: string }) {
  const estilos = usePageStyles();
  const sucessoToast = useSucessoToast();
  const [itensCatalogo, setItensCatalogo] = useState<CatalogoUniforme[]>([]);
  const [tamanhos, setTamanhos] = useState<Record<string, string>>({});
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);

  async function carregar() {
    try {
      setErro(null);
      const [listaItens, listaTamanhos] = await Promise.all([
        api.catalogosUniforme.listar(),
        api.trabalhadores.listarTamanhosUniforme(trabalhadorId),
      ]);
      setItensCatalogo(listaItens);
      const mapa: Record<string, string> = {};
      for (const item of listaTamanhos) mapa[item.catalogoUniformeId] = item.tamanho;
      setTamanhos(mapa);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar tamanhos de uniforme.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trabalhadorId]);

  async function salvar() {
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

  if (carregandoLista || itensCatalogo.length === 0) return null;

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Tamanhos de uniforme</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <Table noNativeElements>
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
    </div>
  );
}
