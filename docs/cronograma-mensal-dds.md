# Cronograma mensal de DDS

Implementado a partir do calendário aprovado pelo usuário em 29/10/2026. As entidades
`CronogramaMensalDds` e `CronogramaMensalDdsRevisao` são uma decisão explícita deste pedido,
fora da base de conhecimento original.

## Uso

Em **Operação → DDS → Cronograma mensal**, selecionar uma obra, abrir um dia, marcar
os temas do catálogo e salvar. É possível pesquisar e cadastrar um tema durante a seleção.
Cada dia admite vários temas; desmarcar todos remove sua programação. A obra acompanha
o seletor global. Os meses e as obras são independentes.

O cronograma é preliminar. Não cria DDS, semanas de DDS, participantes, checklists,
alertas ou obrigações de execução. O usuário pode alterá-lo mesmo depois de emitir o PDF.

O documento é A4 horizontal, com dias da semana, datas e temas. Usa exclusivamente
a logo cadastrada na obra. Sem logo, o planejamento pode ser salvo, mas a prévia e a
emissão ficam bloqueadas. Sábado/domingo são opcionais quando vazios e entram
automaticamente quando há programação nesses dias.

**Visualizar documento** gera uma prévia. **Emitir PDF** guarda o arquivo e uma revisão
imutável com os nomes dos temas, a identidade da obra e sua logo. Emitir novamente
o mesmo conteúdo devolve o mesmo arquivo. A revisão possui QR de rastreabilidade,
sem assinaturas de participantes. Uma edição posterior não altera documentos já emitidos.

## Persistência e acesso

- Agregado único por obra/ano/mês; programação em JSON com datas civis e cópias dos
  títulos selecionados. Alterar ou desativar um tema do catálogo não reescreve dias anteriores.
- Salvamento por versão esperada, com controle de concorrência no EF e índice único no SQL.
  Conflitos devolvem HTTP 409 e conservam a seleção na tela para conferência.
- Emissão transacional: revisão, token e PDF são gravados juntos. Arquivo e logo originais
  permanecem disponíveis no histórico.
- Leitura: `dds:ver`; edição/cadastro de temas: `dds:criar`; PDF: `dds:exportar`.
  Filtro global de obra e resolução de escopo também protegem o histórico e os documentos.
- Migration isolada: `20261029140404_AdicionarCronogramaMensalDds`. Só cria as duas tabelas
  do módulo, seus índices e relacionamentos.

## Calendário Outlook/Teams

A seleção e os indicadores usam datas locais, evitando que `new Date('YYYY-MM-DD')`
desloque um dia em São Paulo. A consulta ao Graph preserva UTC nos limites e a
resposta conserva o instante correto. Falhas do Graph mantêm os vencimentos SST;
detalhes técnicos ficam nos logs e a tela informa a providência necessária.

O erro 403 RAOP ocorre quando a política do Exchange exclui a caixa consultada.
O script `scripts/exchange-autorizar-leitura-calendario.ps1` diagnostica ou adiciona
**Application Calendars.Read para uma única caixa informada**, sem retirar a política
de envio de e-mails. Exige sessão administrativa autenticada no Exchange Online.
Sem `-Aplicar`, o script é somente leitura; suporta `-WhatIf`.

A autorização RBAC e a ApplicationAccessPolicy são independentes. O teste da política
antiga pode continuar negando a caixa, enquanto o RBAC autoriza a leitura do calendário.
A confirmação final é uma chamada real de `calendarView` retornando HTTP 200 após
a propagação, que pode levar de 30 minutos a 2 horas.

Referência: [RBAC para aplicações no Exchange](https://learn.microsoft.com/en-us/exchange/permissions-exo/application-rbac).

## Verificação

- Testes de persistência, isolamento por obra, concorrência, temas desativados,
  emissão idempotente, revisões imutáveis, logo obrigatória e fins de semana.
- PDFs reais de meses com cinco/seis linhas e fevereiro bissexto.
- Playwright contra API real e SQL Server isolado: salvar/reabrir, cadastrar tema,
  prévia, emissão, download da revisão, conflito e datas no fuso America/Sao_Paulo.

Para executar a suíte de navegador, iniciar uma API Development local apontada para
um **banco exclusivo de testes**, aplicar migrations e cadastrar um usuário cujo
`AzureAdObjectId` seja `dev-local-user`. Configurar as três chaves LGPD de teste e a
URL pública de validação local. Não copiar integrações externas ou autenticação real.
Não resetar nem reaproveitar o banco de desenvolvimento habitual.

```powershell
$env:SST_TEST_API = 'http://localhost:5198'
cd src/AAHBRANT.SST.TeamsApp
npx playwright test -c playwright.cronograma.config.ts
```

A suíte cria obras e temas fictícios somente na API local; o servidor Vite usa a porta 5199.
