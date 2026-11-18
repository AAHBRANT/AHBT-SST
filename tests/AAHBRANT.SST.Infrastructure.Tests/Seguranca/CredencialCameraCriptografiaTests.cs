using System.Text;
using AAHBRANT.SST.Infrastructure.Seguranca;

namespace AAHBRANT.SST.Infrastructure.Tests.Seguranca;

// Senha dos equipamentos de câmera IP: AES-256-GCM com chave dedicada. As chaves são injetadas (não mexe no estado estático).
public class CredencialCameraCriptografiaTests
{
    private static byte[] Chave(byte valor) => Enumerable.Repeat(valor, 32).ToArray();

    [Fact]
    public void CifrarEDecifrar_VoltaAoTextoOriginal_ComAcentosESimbolos()
    {
        var servico = new CredencialCameraCriptografiaService(Chave(7));

        var cifrado = servico.Cifrar("Sénha!#@ção 123");

        Assert.DoesNotContain("Sénha", cifrado);
        Assert.Equal("Sénha!#@ção 123", servico.Decifrar(cifrado));
    }

    [Fact]
    public void MesmaSenha_GeraTextosDiferentes_PorCausaDoNonce()
    {
        var servico = new CredencialCameraCriptografiaService(Chave(7));

        Assert.NotEqual(servico.Cifrar("abc"), servico.Cifrar("abc"));
    }

    [Fact]
    public void FormatoCombinaComOPadraoDoCpf_NonceDozeMaisCifraMaisTagDezesseis()
    {
        var bytes = Convert.FromBase64String(new CredencialCameraCriptografiaService(Chave(7)).Cifrar("abcd"));

        Assert.Equal(12 + Encoding.UTF8.GetByteCount("abcd") + 16, bytes.Length);
    }

    [Fact]
    public void ChaveDiferente_NaoDecifra_EDevolveNulo()
    {
        var cifrado = new CredencialCameraCriptografiaService(Chave(7)).Cifrar("abc");

        Assert.Null(new CredencialCameraCriptografiaService(Chave(8)).Decifrar(cifrado));
    }

    [Fact]
    public void TextoCorrompidoOuInvalido_DevolveNulo_SemLancar()
    {
        var servico = new CredencialCameraCriptografiaService(Chave(7));
        var cifrado = Convert.FromBase64String(servico.Cifrar("abc"));
        cifrado[13] ^= 0xFF; // adulterado: a tag GCM não confere

        Assert.Null(servico.Decifrar(Convert.ToBase64String(cifrado)));
        Assert.Null(servico.Decifrar("isto não é base64"));
        Assert.Null(servico.Decifrar("QUJD")); // curto demais
    }

    [Fact]
    public void SemChaveConfigurada_CifrarELancarComMensagemClara()
    {
        var servico = new CredencialCameraCriptografiaService(Array.Empty<byte>());

        var ex = Assert.Throws<InvalidOperationException>(() => servico.Cifrar("abc"));

        Assert.Contains("Lgpd:ChaveCriptografiaCredenciaisCameraBase64", ex.Message);
        Assert.Throws<InvalidOperationException>(() => servico.Decifrar("QUJDREVGR0hJSktMTU5PUFFSU1RVVldYWVo="));
    }
}
