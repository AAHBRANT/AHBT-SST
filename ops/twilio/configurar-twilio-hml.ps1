# Grava as credenciais do Twilio (ligação de alerta da assinatura) na API e no Worker de homologação.
# Rodar pelo responsável, com `az login` feito. Nada secreto é impresso:
#   - o Auth Token vem da área de transferência (copie no Console do Twilio antes de rodar) e ela é limpa;
#   - Account SID e números vão como parâmetros e também viram secrets (o celular é dado pessoal).
#
# Exemplo:
#   & .\ops\twilio\configurar-twilio-hml.ps1 -AccountSid AC... -NumeroOrigem +1555... -NumeroDestino +5583...
# Cada Container App ganha uma revisão nova (reinicia em ~1 minuto).
param(
    [Parameter(Mandatory)][ValidatePattern('^AC[0-9a-fA-F]{32}$')][string]$AccountSid,
    [Parameter(Mandatory)][ValidatePattern('^\+\d{10,15}$')][string]$NumeroOrigem,
    [Parameter(Mandatory)][ValidatePattern('^\+\d{10,15}$')][string]$NumeroDestino
)

$ErrorActionPreference = 'Stop'
$grupo = 'rg-gnezis-hub-staging'

$token = (Get-Clipboard -Raw)
if ($token) { $token = $token.Trim() }
if (-not $token -or $token -notmatch '^[0-9a-fA-F]{32}$') {
    throw 'A área de transferência não tem o Auth Token do Twilio (32 caracteres). Copie no Console do Twilio e rode de novo.'
}
Set-Clipboard -Value ' '

foreach ($app in 'sst-api-hml', 'sst-worker-hml') {
    Write-Host "Gravando no $app..."
    az containerapp secret set --name $app --resource-group $grupo --secrets `
        "twilio-account-sid=$AccountSid" "twilio-auth-token=$token" `
        "twilio-numero-origem=$NumeroOrigem" "twilio-numero-destino=$NumeroDestino" --output none
    az containerapp update --name $app --resource-group $grupo --set-env-vars `
        'Twilio__AccountSid=secretref:twilio-account-sid' 'Twilio__AuthToken=secretref:twilio-auth-token' `
        'Twilio__NumeroOrigem=secretref:twilio-numero-origem' 'Twilio__NumeroDestino=secretref:twilio-numero-destino' --output none
}
$token = $null
Write-Host 'Pronto: Twilio configurado na API e no Worker de hml.'
