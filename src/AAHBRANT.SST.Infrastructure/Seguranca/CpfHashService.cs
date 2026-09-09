using AAHBRANT.SST.Application.Common.Interfaces;

namespace AAHBRANT.SST.Infrastructure.Seguranca;

public class CpfHashService : ICpfHashService
{
    public string CalcularHash(string cpfPlano) => CpfCriptografiaConversor.CalcularHash(cpfPlano);
}
