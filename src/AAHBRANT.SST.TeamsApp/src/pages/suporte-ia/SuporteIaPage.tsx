import { PageHeader } from '@ui';
import { SuporteIaTab } from './SuporteIaTab';

export function SuporteIaPage() {
  return (
    <div>
      <PageHeader
        titulo="SST IA"
        subtitulo="Chamados do G-SST: dúvidas, erros e melhorias, com triagem por IA. Para perguntar, use o botão SST IA no canto da tela."
      />
      <SuporteIaTab />
    </div>
  );
}
