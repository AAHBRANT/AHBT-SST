# SDK Futronic (não versionado)

Copie aqui `ftrScanAPI.dll` (32 bits, assinada pela Futronic) do pacote `ftrScanApiEx_v4.5.zip`
(https://www.futronic-tech.com/download.php). Os `.dll` desta pasta são ignorados pelo git.

Para usar o leitor real: defina `"Leitor": "Futronic"` em `appsettings.json` e publique em 32 bits:

    dotnet publish -c Release -r win-x86 --self-contained
