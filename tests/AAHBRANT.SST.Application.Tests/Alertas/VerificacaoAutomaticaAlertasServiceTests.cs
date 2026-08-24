using AAHBRANT.SST.Application.Alertas;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Tests.Alertas;

// Cobre a lógica introduzida junto com o Worker de alertas automáticos: geração de alerta de
// vencimento (sem duplicar) e escalonamento automático para o Gestor QSMS.
public class VerificacaoAutomaticaAlertasServiceTests
{
    private static Trabalhador CriarTrabalhador(Guid obraId) => new()
    {
        Nome = "Trabalhador Teste",
        Matricula = "MAT-0003",
        Cpf = "22222222222",
        ObraId = obraId,
        FuncaoId = Guid.NewGuid(),
    };

    [Fact]
    public async Task Aso_vencendo_dentro_do_prazo_gera_alerta_de_atencao()
    {
        using var db = DbContextFactory.Criar();
        var obraId = Guid.NewGuid();
        var trabalhador = CriarTrabalhador(obraId);
        db.Trabalhadores.Add(trabalhador);
        db.Asos.Add(new Aso
        {
            TrabalhadorId = trabalhador.Id,
            Tipo = TipoExameAso.Periodico,
            DataExame = DateTime.UtcNow.AddDays(-300),
            DataValidade = DateTime.UtcNow.AddDays(10),
            ResultadoStatus = ResultadoAso.Apto,
        });
        await db.SaveChangesAsync();

        var servico = new VerificacaoAutomaticaAlertasService(db);
        var criados = await servico.VerificarVencimentosAsync(diasAntecedencia: 30, CancellationToken.None);

        Assert.Equal(1, criados);
        var alerta = Assert.Single(db.Alertas);
        Assert.Equal(TipoAlerta.AsoVencendo, alerta.Tipo);
        Assert.Equal(SeveridadeAlerta.Atencao, alerta.Severidade);
        Assert.Equal(obraId, alerta.ObraId!.Value);
    }

    [Fact]
    public async Task Aso_ja_vencido_gera_alerta_critico()
    {
        using var db = DbContextFactory.Criar();
        var trabalhador = CriarTrabalhador(Guid.NewGuid());
        db.Trabalhadores.Add(trabalhador);
        db.Asos.Add(new Aso
        {
            TrabalhadorId = trabalhador.Id,
            Tipo = TipoExameAso.Periodico,
            DataExame = DateTime.UtcNow.AddDays(-400),
            DataValidade = DateTime.UtcNow.AddDays(-5),
            ResultadoStatus = ResultadoAso.Apto,
        });
        await db.SaveChangesAsync();

        var servico = new VerificacaoAutomaticaAlertasService(db);
        await servico.VerificarVencimentosAsync(diasAntecedencia: 30, CancellationToken.None);

        var alerta = Assert.Single(db.Alertas);
        Assert.Equal(TipoAlerta.AsoVencido, alerta.Tipo);
        Assert.Equal(SeveridadeAlerta.Critico, alerta.Severidade);
    }

    [Fact]
    public async Task Nao_duplica_alerta_se_ja_existir_um_ativo_para_o_mesmo_aso()
    {
        using var db = DbContextFactory.Criar();
        var trabalhador = CriarTrabalhador(Guid.NewGuid());
        db.Trabalhadores.Add(trabalhador);
        var aso = new Aso
        {
            TrabalhadorId = trabalhador.Id,
            Tipo = TipoExameAso.Periodico,
            DataExame = DateTime.UtcNow.AddDays(-300),
            DataValidade = DateTime.UtcNow.AddDays(10),
            ResultadoStatus = ResultadoAso.Apto,
        };
        db.Asos.Add(aso);
        await db.SaveChangesAsync();

        var servico = new VerificacaoAutomaticaAlertasService(db);
        var primeiraExecucao = await servico.VerificarVencimentosAsync(30, CancellationToken.None);
        var segundaExecucao = await servico.VerificarVencimentosAsync(30, CancellationToken.None);

        Assert.Equal(1, primeiraExecucao);
        Assert.Equal(0, segundaExecucao); // já existe alerta ativo — não cria de novo
        Assert.Single(db.Alertas);
    }

    [Fact]
    public async Task Cria_novo_alerta_se_o_anterior_ja_foi_resolvido()
    {
        using var db = DbContextFactory.Criar();
        var trabalhador = CriarTrabalhador(Guid.NewGuid());
        db.Trabalhadores.Add(trabalhador);
        var aso = new Aso
        {
            TrabalhadorId = trabalhador.Id,
            Tipo = TipoExameAso.Periodico,
            DataExame = DateTime.UtcNow.AddDays(-300),
            DataValidade = DateTime.UtcNow.AddDays(10),
            ResultadoStatus = ResultadoAso.Apto,
        };
        db.Asos.Add(aso);
        db.Alertas.Add(new Alerta
        {
            Tipo = TipoAlerta.AsoVencendo,
            Severidade = SeveridadeAlerta.Atencao,
            Titulo = "Alerta antigo já resolvido",
            EntidadeOrigemTipo = "Aso",
            EntidadeOrigemId = aso.Id,
            Status = StatusAlerta.Resolvido,
        });
        await db.SaveChangesAsync();

        var servico = new VerificacaoAutomaticaAlertasService(db);
        var criados = await servico.VerificarVencimentosAsync(30, CancellationToken.None);

        Assert.Equal(1, criados);
        Assert.Equal(2, db.Alertas.Count());
    }

    [Fact]
    public async Task Aso_fora_do_prazo_de_antecedencia_nao_gera_alerta()
    {
        using var db = DbContextFactory.Criar();
        var trabalhador = CriarTrabalhador(Guid.NewGuid());
        db.Trabalhadores.Add(trabalhador);
        db.Asos.Add(new Aso
        {
            TrabalhadorId = trabalhador.Id,
            Tipo = TipoExameAso.Periodico,
            DataExame = DateTime.UtcNow.AddDays(-10),
            DataValidade = DateTime.UtcNow.AddDays(300), // vence daqui a quase 1 ano
            ResultadoStatus = ResultadoAso.Apto,
        });
        await db.SaveChangesAsync();

        var servico = new VerificacaoAutomaticaAlertasService(db);
        var criados = await servico.VerificarVencimentosAsync(diasAntecedencia: 30, CancellationToken.None);

        Assert.Equal(0, criados);
        Assert.Empty(db.Alertas);
    }

    private static (Usuario usuario, PerfilAcesso perfil) CriarGestorQsms(Guid? obraDoVinculo)
    {
        var usuario = new Usuario
        {
            AzureAdObjectId = Guid.NewGuid().ToString(),
            Email = "gestor@aahbrant.com",
            Nome = "Gestor QSMS Teste",
            Status = StatusUsuario.Ativo,
        };
        var perfil = new PerfilAcesso { Tipo = TipoPerfilAcesso.GestorQsms, Nome = "Gestor QSMS", EhSistema = true };
        return (usuario, perfil);
    }

    [Fact]
    public async Task Escalona_alerta_vencido_para_gestor_qsms_da_obra()
    {
        using var db = DbContextFactory.Criar();
        var obraId = Guid.NewGuid();
        var (gestor, perfil) = CriarGestorQsms(obraId);
        db.Usuarios.Add(gestor);
        db.PerfisAcesso.Add(perfil);
        db.UsuariosPerfilObra.Add(new UsuarioPerfilObra { UsuarioId = gestor.Id, PerfilAcessoId = perfil.Id, ObraId = obraId });
        db.Alertas.Add(new Alerta
        {
            Tipo = TipoAlerta.AsoVencido,
            Severidade = SeveridadeAlerta.Critico,
            Titulo = "ASO vencido",
            EntidadeOrigemTipo = "Aso",
            EntidadeOrigemId = Guid.NewGuid(),
            ObraId = obraId,
            Status = StatusAlerta.Aberto,
            DataLimiteTratamento = DateTime.UtcNow.AddDays(-1), // prazo já passou
        });
        await db.SaveChangesAsync();

        var servico = new VerificacaoAutomaticaAlertasService(db);
        var escalonados = await servico.EscalonarPendentesAsync(CancellationToken.None);

        Assert.Equal(1, escalonados);
        var alerta = Assert.Single(db.Alertas);
        Assert.Equal(StatusAlerta.Escalonado, alerta.Status);
        Assert.Equal(gestor.Id, alerta.EscalonadoParaUsuarioId!.Value);
        Assert.NotNull(alerta.DataEscalonamento);
    }

    [Fact]
    public async Task Nao_escalona_alerta_cujo_prazo_ainda_nao_passou()
    {
        using var db = DbContextFactory.Criar();
        db.Alertas.Add(new Alerta
        {
            Tipo = TipoAlerta.AsoVencendo,
            Severidade = SeveridadeAlerta.Atencao,
            Titulo = "ASO vencendo",
            EntidadeOrigemTipo = "Aso",
            EntidadeOrigemId = Guid.NewGuid(),
            Status = StatusAlerta.Aberto,
            DataLimiteTratamento = DateTime.UtcNow.AddDays(5), // ainda no prazo
        });
        await db.SaveChangesAsync();

        var servico = new VerificacaoAutomaticaAlertasService(db);
        var escalonados = await servico.EscalonarPendentesAsync(CancellationToken.None);

        Assert.Equal(0, escalonados);
        Assert.Equal(StatusAlerta.Aberto, Assert.Single(db.Alertas).Status);
    }

    [Fact]
    public async Task Sem_gestor_qsms_cadastrado_alerta_fica_pendente_sem_erro()
    {
        using var db = DbContextFactory.Criar();
        db.Alertas.Add(new Alerta
        {
            Tipo = TipoAlerta.AsoVencido,
            Severidade = SeveridadeAlerta.Critico,
            Titulo = "ASO vencido",
            EntidadeOrigemTipo = "Aso",
            EntidadeOrigemId = Guid.NewGuid(),
            Status = StatusAlerta.Aberto,
            DataLimiteTratamento = DateTime.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();

        var servico = new VerificacaoAutomaticaAlertasService(db);
        var escalonados = await servico.EscalonarPendentesAsync(CancellationToken.None);

        Assert.Equal(0, escalonados);
        Assert.Equal(StatusAlerta.Aberto, Assert.Single(db.Alertas).Status);
    }
}
