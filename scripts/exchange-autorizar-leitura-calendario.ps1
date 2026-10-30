[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string] $Caixa,
    [Parameter(Mandatory)][Guid] $AppId,
    [switch] $Aplicar
)

# Execute em uma sessão Connect-ExchangeOnline autenticada por um administrador.
# A autorização é SOMENTE Calendars.Read para a caixa explicitamente informada.
# Mantém ApplicationAccessPolicy de Mail.Send e quaisquer escopos existentes.
# Sem -Aplicar, apenas diagnostica. -WhatIf apresenta as inclusões propostas.
$ErrorActionPreference = 'Stop'
$destino = Get-Recipient -Identity $Caixa
$email = $destino.PrimarySmtpAddress.ToString()
if ($email -notmatch '^[a-zA-Z0-9._+@-]+$') { throw 'Endereço de caixa não aceito para o filtro de escopo.' }
$sp = @(Get-ServicePrincipal | Where-Object { $_.AppId -eq $AppId.ToString() })
if ($sp.Count -ne 1) { throw 'Cadastre primeiro o service principal desta aplicação no Exchange e repita o diagnóstico.' }
$nome = "SST Calendario Read $email"
$filtro = "PrimarySmtpAddress -eq '$email'"
$escopo = Get-ManagementScope -Identity $nome -ErrorAction SilentlyContinue
if ($escopo -and $escopo.RecipientFilter -ne $filtro -and $escopo.RecipientFilter -ne "($filtro)") {
    throw 'Já existe um escopo com este nome e outro filtro. Nenhuma alteração foi feita.'
}
if ($Aplicar) {
    if (!$escopo -and $PSCmdlet.ShouldProcess($email, 'Criar escopo de leitura de uma única caixa')) {
        New-ManagementScope -Name $nome -RecipientRestrictionFilter $filtro | Out-Null
    }
    $atribuicao = Get-ManagementRoleAssignment -Identity $nome -ErrorAction SilentlyContinue
    if ($atribuicao -and ($atribuicao.Role -ne 'Application Calendars.Read' -or $atribuicao.RoleAssignee.ToString() -ne $sp[0].Identity.ToString())) {
        throw 'Já existe uma atribuição com este nome e outra autorização. Confira manualmente.'
    }
    if (!$atribuicao -and $PSCmdlet.ShouldProcess($email, 'Autorizar Application Calendars.Read para a aplicação SST')) {
        New-ManagementRoleAssignment -Name $nome -Role 'Application Calendars.Read' -App $sp[0].Identity -CustomResourceScope $nome | Out-Null
    }
}
Test-ServicePrincipalAuthorization -Identity $sp[0].Identity -Resource $email |
    Select-Object RoleName, GrantedPermissions, AllowedResourceScope, InScope
Test-ApplicationAccessPolicy -AppId $AppId -Identity $email |
    Select-Object AppId, Mailbox, AccessCheckResult
