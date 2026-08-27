using System.Runtime.CompilerServices;
using AAHBRANT.SST.Infrastructure.Seguranca;

namespace AAHBRANT.SST.Infrastructure.Tests;

// SstDbContext.AplicarAuditoria calcula CpfHash em todo SaveChanges que toque um Trabalhador — sem
// isso configurado, qualquer teste que salve um Trabalhador com Cpf preenchido lança
// InvalidOperationException. Roda uma única vez, antes de qualquer teste do assembly.
internal static class TestesModuleInitializer
{
    [ModuleInitializer]
    public static void Inicializar()
    {
        var chaveFake = new byte[32];
        Array.Fill(chaveFake, (byte)1);
        CpfCriptografiaContexto.Configurar(chaveFake, chaveFake);

        var chaveBiometriaFake = new byte[32];
        Array.Fill(chaveBiometriaFake, (byte)2);
        TemplateBiometricoCriptografiaContexto.Configurar(chaveBiometriaFake);
    }
}
