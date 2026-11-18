namespace AAHBRANT.SST.Application.Cameras;

// Cifra e decifra a senha dos equipamentos de câmera (AES-256-GCM, chave dedicada
// Lgpd:ChaveCriptografiaCredenciaisCameraBase64: não reaproveita a chave de CPF nem a de biometria). Quem só grava
// a senha depende desta interface, mas o único lugar que chama Decifrar é a configuração entregue ao agente.
public interface ICredencialCameraCriptografia
{
    string Cifrar(string senhaPlana);

    // Texto que não decifra (chave trocada, dado corrompido) volta nulo: quem chama trata como "senha não definida".
    string? Decifrar(string senhaCifrada);
}
