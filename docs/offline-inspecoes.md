# Inspeções no campo: criação e continuidade offline

Antes de ir a campo, abra a aba de inspeções com internet e aguarde o indicador de modelos
preparados neste dispositivo. A preparação baixa modelos, obras e responsáveis para a conta
em uso. Com esses dados baixados, o botão **Nova inspeção** cria o registro e seus itens no tablet,
mesmo sem rede. Inspeções existentes precisam ser abertas uma vez com internet para baixar o detalhe.

- Digitar grava um rascunho local. **Salvar item** inclui a resposta na fila e atualiza o progresso.
- Fotos ficam vinculadas ao item com o arquivo original e os metadados de captura. Uma foto com
  localização pendente fica como rascunho, sem envio e sem permitir a finalização.
- A finalização permanece pendente até respostas e fotos serem aceitas pelo servidor.
- Na criação offline, identificadores GUID da inspeção e dos itens são gerados localmente e
  validados no servidor contra o modelo. A criação é enviada antes de qualquer resposta/foto;
  o protocolo e as versões iniciais são gerados no servidor. A resposta idempotente guarda
  esses vínculos para não duplicar a inspeção se a conexão cair durante a confirmação.
- Modelos incompletos, identificadores em uso ou versão divergente não são aceitos silenciosamente.
  A fila e os arquivos locais permanecem para revisão. Descartar uma criação local pede confirmação
  explícita, pois remove também os dados e anexos ainda não enviados daquela inspeção.
- Alojamentos e veículos mantêm seus fluxos específicos de criação online; o painel genérico
  não cria inspeções desses tipos. Assinaturas e geração de PDF continuam exigindo conexão.
- Reabra/mantenha a PWA aberta ao voltar a ter rede. A fila usa a conta que registrou a ação;
  uma troca de conta não envia dados em nome da outra pessoa. A identidade local não é um token
  de acesso nem substitui a validação de autorização do servidor.
- Falhas temporárias e ausência de login preservam a fila. Recusas ficam no indicador de
  sincronização com opções de revisar/reaplicar ou descartar. A finalização aguarda a resolução.
- As respostas expõem a RowVersion existente. Cada mutação pode informar a versão esperada;
  divergências retornam 409 sem sobrescrever o servidor. A versão devolvida no corpo da resposta
  permite avançar a sequência local e também é preservada pelo middleware de idempotência.
- As migrações IndexedDB acrescentam tabelas para as inspeções e sua preparação, mantendo as filas e os caches anteriores.

Verificação automatizada:

```text
npm run test:inspecao-offline
npm run test:pwa
dotnet test tests/AAHBRANT.SST.Application.Tests --filter FullyQualifiedName~Inspecoes
```

Os testes de navegador usam API simulada e IndexedDB real, incluindo rascunho após recarga,
fotos/metadata, falha temporária, 401, conflito e troca de conta. O teste de PWA usa build de
produção e desliga a rede do navegador. A câmera, o GPS e o login Entra reais exigem validação
no tablet. Não limpar os dados da PWA enquanto houver rascunhos ou ações pendentes.
