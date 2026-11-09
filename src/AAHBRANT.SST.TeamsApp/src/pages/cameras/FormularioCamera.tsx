import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Button, Campo, FeedbackInline, Field, FormGrid, FormRodape, FormSection, Input, Select, Switch } from '@ui';
import { api, TipoCamera, tipoCameraLabel, type CameraResumo, type DadosCamera, type Obra } from '../../lib/api';

const INTERVALOS_PADRAO = [0, 5, 10, 30, 60];

function rotuloIntervalo(minutos: number): string {
  return minutos === 0 ? 'Sem limite — alerta a cada ocorrência' : `${minutos} minutos`;
}

export interface FormularioCameraProps {
  camera?: CameraResumo;
  obras: Obra[];
  obraInicial?: string;
  aoSalvar: (obraId: string) => void;
  aoCancelar: () => void;
}

export function FormularioCamera({ camera, obras, obraInicial, aoSalvar, aoCancelar }: FormularioCameraProps) {
  const edicao = !!camera;
  const [obraId, setObraId] = useState(camera?.obraId ?? obraInicial ?? '');
  const [nome, setNome] = useState(camera?.nome ?? '');
  const [areaSstId, setAreaSstId] = useState(camera?.areaSstId ?? '');
  const [tipo, setTipo] = useState<number>(camera?.tipo ?? TipoCamera.Canteiro);
  const [habilitada, setHabilitada] = useState(camera?.habilitada ?? true);
  const [intervalo, setIntervalo] = useState(camera?.intervaloMinimoAlertasMinutos ?? 0);
  const [erro, setErro] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);

  const areas = useQuery({
    queryKey: ['areasSst', 'lista', obraId],
    queryFn: () => api.areasSst.listar(obraId),
    enabled: !!obraId,
  });
  const listaAreas = obraId ? (areas.data ?? []) : [];
  // Câmera antiga pode apontar para uma área que a consulta ainda não trouxe: mantém a opção para não perdê-la.
  const areaAtualFaltando = !!areaSstId && !listaAreas.some((a) => a.id === areaSstId);

  // Intervalo gravado fora da lista padrão continua selecionável em vez de ser trocado em silêncio.
  const intervalos = INTERVALOS_PADRAO.includes(intervalo)
    ? INTERVALOS_PADRAO
    : [...INTERVALOS_PADRAO, intervalo].sort((a, b) => a - b);

  async function salvar() {
    const nomeLimpo = nome.trim();
    if (!obraId) return setErro('Selecione a obra da câmera.');
    if (!nomeLimpo) return setErro('Informe o nome da câmera.');
    if (nomeLimpo.length > 100) return setErro('O nome da câmera pode ter no máximo 100 caracteres.');

    const dados: DadosCamera = {
      obraId,
      nome: nomeLimpo,
      areaSstId: areaSstId || null,
      tipo,
      habilitada,
      intervaloMinimoAlertasMinutos: intervalo,
    };
    try {
      setSalvando(true);
      setErro(null);
      if (camera) {
        const { obraId: _obra, ...semObra } = dados;
        void _obra;
        await api.cameras.atualizar(camera.id, semObra);
      } else {
        await api.cameras.criar(dados);
      }
      aoSalvar(obraId);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível salvar a câmera.');
    } finally {
      setSalvando(false);
    }
  }

  return (
    <FormSection titulo="Dados da câmera" numero={1} primeira>
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      <FormGrid>
        <Campo span={6}>
          <Field label="Obra" required>
            <Select
              value={obraId}
              disabled={edicao}
              onChange={(_, d) => {
                setObraId(d.value);
                setAreaSstId('');
              }}
            >
              <option value="">Selecione</option>
              {obras.map((obra) => (
                <option key={obra.id} value={obra.id}>
                  {obra.nome}
                </option>
              ))}
            </Select>
          </Field>
        </Campo>
        <Campo span={6}>
          <Field label="Nome" required>
            <Input
              value={nome}
              maxLength={100}
              placeholder="Ex.: Betoneira"
              onChange={(_, d) => setNome(d.value)}
            />
          </Field>
        </Campo>
        <Campo span={6}>
          <Field label="Área">
            <Select value={areaSstId} disabled={!obraId} onChange={(_, d) => setAreaSstId(d.value)}>
              <option value="">Sem área definida</option>
              {areaAtualFaltando && <option value={areaSstId}>{camera?.areaNome ?? 'Área atual'}</option>}
              {listaAreas.map((area) => (
                <option key={area.id} value={area.id}>
                  {area.nome}
                </option>
              ))}
            </Select>
          </Field>
        </Campo>
        <Campo span={6}>
          <Field label="Tipo" required>
            <Select value={String(tipo)} onChange={(_, d) => setTipo(Number(d.value))}>
              {[TipoCamera.Canteiro, TipoCamera.Portaria].map((t) => (
                <option key={t} value={t}>
                  {tipoCameraLabel[t]}
                </option>
              ))}
            </Select>
          </Field>
        </Campo>
        <Campo span={6}>
          <Field label="Intervalo entre alertas">
            <Select value={String(intervalo)} onChange={(_, d) => setIntervalo(Number(d.value))}>
              {intervalos.map((minutos) => (
                <option key={minutos} value={minutos}>
                  {rotuloIntervalo(minutos)}
                </option>
              ))}
            </Select>
          </Field>
        </Campo>
        <Campo span={6}>
          <Switch
            checked={habilitada}
            label="Monitoramento ligado"
            onChange={(_, d) => setHabilitada(d.checked)}
          />
        </Campo>
      </FormGrid>
      <FormRodape>
        <Button onClick={aoCancelar} disabled={salvando}>
          Cancelar
        </Button>
        <Button appearance="primary" onClick={salvar} disabled={salvando}>
          {edicao ? 'Salvar alterações' : 'Cadastrar câmera'}
        </Button>
      </FormRodape>
    </FormSection>
  );
}
