# Manifest do Teams — status

Este `manifest.json` é um **esqueleto para desenvolvimento local**, não está pronto para sideload real.

## Placeholders que precisam de valor real (todos marcados com `SUBSTITUIR-`)

- `developer.websiteUrl` / `privacyUrl` / `termsOfUseUrl`, `staticTabs[].contentUrl`,
  `configurableTabs[].configurationUrl`, `validDomains` — dependem do domínio real do
  **Azure App Service** que hospedará a Tab (provisionamento real de recursos Azure requer
  confirmação explícita sua, passo a passo, conforme o plano aprovado).
- `webApplicationInfo.id` / `resource` — dependem do **App Registration (Entra ID)** real, que
  também só é criado com sua confirmação explícita.

## Pendente: ícones

Teams exige dois ícones que ainda não existem no repositório:

- `color.png` — 192×192px, colorido, fundo pode ser sólido.
- `outline.png` — 32×32px, monocromático (branco/transparente), sem fundo.

Os logos hoje disponíveis na raiz do projeto (`logo AHBT natural.png`,
`logo_AHBT_natural-removebg-preview.png`) são retangulares (883×190) e não servem diretamente —
precisam ser recortados/redimensionados para os formatos acima antes do sideload.

## Como isso evolui

1. Quando o App Service e o App Registration existirem (com sua confirmação), substituir os
   valores `SUBSTITUIR-*` pelos reais.
2. Gerar `color.png`/`outline.png` a partir da marca AAHBRANT.
3. Validar o manifest com `teamsapp validate` (Teams Toolkit) ou o App Studio/Developer Portal do
   Teams antes do sideload.
