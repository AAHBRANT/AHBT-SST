# Grava o token do robô só de alertas (Telegram:AlertasBotToken) na API e no Worker de homologação.
# Rodar pelo responsável, com `az login` feito. O token vem da área de transferência (copie no @BotFather
# antes de rodar) e ela é limpa em seguida; nada é impresso. Cada Container App reinicia em ~1 minuto.
# Antes: mandar /start ao robô novo, senão o Telegram não deixa ele escrever para você.
$ErrorActionPreference = 'Stop'
$grupo = 'rg-gnezis-hub-staging'

$token = (Get-Clipboard -Raw)
if ($token) { $token = $token.Trim() }
if (-not $token -or $token -notmatch '^\d{6,12}:[A-Za-z0-9_-]{30,}$') {
    throw 'A área de transferência não tem um token de robô do Telegram. Copie o token no @BotFather e rode de novo.'
}
Set-Clipboard -Value ' '

foreach ($app in 'sst-api-hml', 'sst-worker-hml') {
    Write-Host "Gravando no $app..."
    az containerapp secret set --name $app --resource-group $grupo --secrets "telegram-alertas-bot-token=$token" --output none
    az containerapp update --name $app --resource-group $grupo `
        --set-env-vars 'Telegram__AlertasBotToken=secretref:telegram-alertas-bot-token' --output none
}
$token = $null
Write-Host 'Pronto: robô de alertas configurado na API e no Worker de hml.'
