import { PageHeader } from '@ui';
import { SuporteIaTab } from './SuporteIaTab';

export function SuporteIaPage() {
  return (
    <div>
      <PageHeader
        titulo="Central de Suporte IA"
        subtitulo="Triagem governada para dúvidas, bugs, melhorias e demandas técnicas do SST."
      />
      <SuporteIaTab />
    </div>
  );
}
