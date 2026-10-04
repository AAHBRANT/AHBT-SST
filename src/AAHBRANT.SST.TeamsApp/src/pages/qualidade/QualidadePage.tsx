import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Button, Card, EstadoVazio, FeedbackInline, Field, PageHeader, Select, Spinner } from '@ui';
import { plataformaApi, type ContextoModulo, type ModuloPlataforma, type ObraModulo } from '../../lib/plataformaApi';
import { useUsuarioLogado } from '../../lib/UsuarioLogadoContext';
import './plataforma.css';

export function QualidadePage({ modulo = 'qualidade' }: { modulo?: ModuloPlataforma }) {
  const [params, setParams] = useSearchParams();
  const obraId = params.get('obraId') ?? '';
  const [obras, setObras] = useState<ObraModulo[]>([]);
  const [contexto, setContexto] = useState<ContextoModulo | null>(null);
  const [erro, setErro] = useState('');
  const [carregando, setCarregando] = useState(true);
  const [tentativa, setTentativa] = useState(0);
  const { usuario } = useUsuarioLogado();
  const navigate = useNavigate();
  const titulo = modulo === 'qualidade' ? 'Qualidade' : 'SST por obra';

  useEffect(() => {
    const controller = new AbortController();
    setCarregando(true);
    setErro('');
    setContexto(null);
    setObras([]);
    Promise.all([
      plataformaApi.obras(modulo, false, controller.signal),
      obraId ? plataformaApi.contexto(modulo, obraId, controller.signal) : Promise.resolve(null),
    ]).then(([lista, atual]) => {
      if (!controller.signal.aborted) { setObras(lista); setContexto(atual); }
    }).catch((e: unknown) => {
      if (!controller.signal.aborted) setErro(e instanceof Error ? e.message : 'Não foi possível carregar a obra.');
    }).finally(() => { if (!controller.signal.aborted) setCarregando(false); });
    return () => controller.abort();
  }, [modulo, obraId, tentativa]);

  return <div className="plataforma-pagina">
    <PageHeader titulo={titulo} subtitulo="Acompanhe o módulo nas obras às quais você tem acesso." />
    {carregando ? <Spinner label="Consultando acesso e obras…" /> : erro ? <>
      <FeedbackInline tom="erro">{erro}</FeedbackInline>
      <div className="plataforma-acoes"><Button onClick={() => setTentativa(t => t + 1)}>Tentar novamente</Button>
        {obraId && <Button onClick={() => setParams({})}>Voltar às minhas obras</Button>}</div>
    </> : obras.length === 0 ? <EstadoVazio titulo="Nenhuma obra disponível" descricao="Seu perfil permite o módulo, mas não há obras ativas disponíveis para consulta." /> : <>
      <Card titulo="Selecionar obra">
        <Field label="Obra"><Select value={obraId} onChange={(_, data) => setParams(data.value ? { obraId: data.value } : {})}>
          <option value="">Selecione uma obra</option>
          {obras.map(obra => <option key={obra.id} value={obra.id}>{obra.codigo} · {obra.nome}</option>)}
        </Select></Field>
      </Card>
      {contexto && <Card titulo={`${contexto.obra.codigo} · ${contexto.obra.nome}`} subtitulo={[contexto.obra.cidade, contexto.obra.uf].filter(Boolean).join(' / ')}>
        <p>Obra selecionada para {modulo === 'qualidade' ? 'Qualidade' : 'SST'}.</p>
        {contexto.podeConfigurarTeams && <FeedbackInline tom="info">Você pode adicionar esta obra como uma aba do G-SST em um canal ou chat do Teams.</FeedbackInline>}
        <div className="plataforma-acoes">
          {usuario?.permissoes.includes('calendario:ver') && <Button onClick={() => navigate('/calendario')}>Abrir calendário de SST</Button>}
          {modulo === 'sst' && <Button onClick={() => navigate('/gestao-sst')}>Abrir Gestão de SST</Button>}
        </div>
      </Card>}
    </>}
  </div>;
}
