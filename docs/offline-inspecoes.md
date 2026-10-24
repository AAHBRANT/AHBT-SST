# Inspeções iniciadas online: continuidade offline

Abra a inspeção com internet na PWA antes de ir a campo. O detalhe e as fotos já consultadas
ficam no IndexedDB do navegador. Esta entrega não cria inspeções novas sem conexão.

- Digitar grava um rascunho local. **Salvar item** inclui a resposta na fila e atualiza o progresso.
- Fotos ficam vinculadas ao item com o arquivo original e os metadados de captura. Uma foto com
  localização pendente fica como rascunho, sem envio e sem permitir a finalização.
- A finalização permanece pendente até respostas e fotos serem aceitas pelo servidor.
- Reabra/mantenha a PWA aberta ao voltar a ter rede. A fila usa a conta que registrou a ação;
  uma troca de conta não envia dados em nome da outra pessoa. A identidade local não é um token
  de acesso nem substitui a validação de autorização do servidor.
- Falhas temporárias e ausência de login preservam a fila. Recusas ficam no indicador de
  sincronização com opções de revisar/reaplicar ou descartar. A finalização aguarda a resolução.
- As respostas expõem a RowVersion existente. Cada mutação pode informar a versão esperada;
  divergências retornam 409 sem sobrescrever o servidor. A versão devolvida no corpo da resposta
  permite avançar a sequência local e também é preservada pelo middleware de idempotência.
- A migração IndexedDB acrescenta uma tabela, mantendo as filas e os caches anteriores.

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
