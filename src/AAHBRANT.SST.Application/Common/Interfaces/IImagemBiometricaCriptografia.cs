namespace AAHBRANT.SST.Application.Common.Interfaces;

// Criptografia (AES-GCM, mesma chave LGPD de biometria) das IMAGENS biométricas de referência, como a
// imagem da digital do cadastro. Diferente do template, que o backend nunca descriptografa (só o
// agente local), a imagem precisa ser lida pelo servidor para sair no log de assinaturas da Ficha de
// EPI. Por isso só este contrato expõe Descriptografar, e só imagens passam por ele.
public interface IImagemBiometricaCriptografia
{
    string Criptografar(byte[] imagem);
    byte[] Descriptografar(string cifradoBase64);
}
