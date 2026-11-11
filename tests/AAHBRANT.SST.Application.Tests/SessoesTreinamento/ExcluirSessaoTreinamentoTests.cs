using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.SessoesTreinamento.Commands;
using AAHBRANT.SST.Application.SessoesTreinamento.Queries;
using AAHBRANT.SST.Application.Tests.TestSupport;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Infrastructure.Persistencia;
using AAHBRANT.SST.Infrastructure.Seguranca;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Tests.SessoesTreinamento;

// Exclusão definitiva de turma (10/11/2026): a turma, as inscrições e as fotos saem do banco; turma com certificado
// (mesmo excluído logicamente) ou com documento assinado é recusada; a trilha registra o retrato; nenhuma outra exclusão
// do sistema deixa de ser lógica.
public class ExcluirSessaoTreinamentoTests
{
    private sealed class AuditoriaGravando(IAppDbContext db) : IAuditoriaService
    {
        public Task RegistrarAsync(string acao, string entidadeTipo, Guid entidadeId, Guid? usuarioId, Guid? trabalhadorId, object? dadosDepois, CancellationToken ct)
        {
            db.TrilhaAuditoria.Add(new TrilhaAuditoria
            {
                Timestamp = DateTime.UtcNow, Acao = acao, EntidadeTipo = entidadeTipo, EntidadeId = entidadeId, UsuarioId = usuarioId,
                DadosDepoisJson = System.Text.Json.JsonSerializer.Serialize(dadosDepois),
                HashRegistroAnterior = "x", HashRegistroAtual = "y",
            });
            return Task.CompletedTask;
        }
    }

    private static readonly Guid ObraA = Guid.NewGuid();
    private static readonly Guid ObraB = Guid.NewGuid();

    private static SstDbContext NovoContexto(string banco, ICurrentUserService? usuario = null)
    {
        ChavesCpfDeTeste.Configurar();
        var opcoes = new DbContextOptionsBuilder<SstDbContext>().UseInMemoryDatabase(banco).Options;
        return new SstDbContext(opcoes, usuario ?? new CurrentUserService());
    }

    private static async Task<SessaoTreinamento> CriarTurmaAsync(SstDbContext db, Guid obraId, int participantes = 2, int fotos = 3)
    {
        var obra = await db.Obras.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == obraId);
        if (obra is null)
        {
            obra = new Obra { Id = obraId, Codigo = obraId.ToString("N")[..6], Nome = "Obra " + obraId.ToString("N")[..4] };
            db.Obras.Add(obra);
        }
        var funcao = new Funcao { Nome = "Ajudante Geral" };
        var curso = new CursoTreinamento { Nome = "NR-35 Trabalho em Altura", NormaReferencia = "NR-35", ValidadeEmMeses = 24 };
        var sessao = new SessaoTreinamento
        {
            ObraId = obraId, CursoTreinamento = curso, DataRealizacao = new DateTime(2026, 11, 10), CargaHorariaRealizada = 8,
            NumeroCertificado = "TRN-2026-0187",
        };
        for (var i = 0; i < participantes; i++)
        {
            var t = new Trabalhador { ObraId = obraId, Funcao = funcao, Nome = $"Trabalhador {i}", Cpf = $"1000000000{i}" };
            db.Trabalhadores.Add(t);
            sessao.Participantes.Add(new ParticipanteSessaoTreinamento
            {
                TrabalhadorId = t.Id,
                PresencaConfirmadaEm = i == 0 ? DateTime.UtcNow : null,
                EvidenciaConteudo = new byte[] { 1, 2, 3 },
            });
        }
        for (var i = 1; i <= fotos; i++)
            sessao.FotosEvidencia.Add(new FotoEvidenciaSessaoTreinamento { Ordem = i, FotoConteudo = new byte[] { 9 }, FotoContentType = "image/jpeg" });
        db.SessoesTreinamento.Add(sessao);
        await db.SaveChangesAsync();
        return sessao;
    }

    private static Task<List<ResultadoExclusaoTurma>> ExcluirLoteAsync(SstDbContext db, params Guid[] ids)
        => new ExcluirSessoesTreinamentoLoteCommandHandler(db, new AuditoriaGravando(db), new CurrentUserService())
            .Handle(new ExcluirSessoesTreinamentoLoteCommand(ids), CancellationToken.None);

    private static Task ExcluirAsync(SstDbContext db, Guid id)
        => new ExcluirSessaoTreinamentoCommandHandler(db, new AuditoriaGravando(db), new CurrentUserService())
            .Handle(new ExcluirSessaoTreinamentoCommand(id), CancellationToken.None);

    private static async Task<Treinamento> AdicionarCertificadoAsync(SstDbContext db, SessaoTreinamento sessao)
    {
        var trabalhadorId = sessao.Participantes.First().TrabalhadorId;
        var cert = new Treinamento
        {
            TrabalhadorId = trabalhadorId, CursoTreinamentoId = sessao.CursoTreinamentoId, SessaoTreinamentoId = sessao.Id,
            DataRealizacao = sessao.DataRealizacao, DataValidade = sessao.DataRealizacao.AddYears(2),
        };
        db.Treinamentos.Add(cert);
        await db.SaveChangesAsync();
        return cert;
    }

    [Fact]
    public async Task TurmaSemCertificado_SaiDoBancoComParticipantesEFotos()
    {
        var db = NovoContexto(Guid.NewGuid().ToString());
        var sessao = await CriarTurmaAsync(db, ObraA);
        var outra = await CriarTurmaAsync(db, ObraA, participantes: 1, fotos: 1);

        await ExcluirAsync(db, sessao.Id);

        Assert.False(await db.SessoesTreinamento.IgnoreQueryFilters().AnyAsync(s => s.Id == sessao.Id));
        Assert.False(await db.ParticipantesSessaoTreinamento.IgnoreQueryFilters().AnyAsync(p => p.SessaoTreinamentoId == sessao.Id));
        Assert.False(await db.FotosEvidenciaSessaoTreinamento.IgnoreQueryFilters().AnyAsync(f => f.SessaoTreinamentoId == sessao.Id));
        // A outra turma e os filhos dela seguem intactos.
        Assert.True(await db.SessoesTreinamento.IgnoreQueryFilters().AnyAsync(s => s.Id == outra.Id));
        Assert.Equal(1, await db.ParticipantesSessaoTreinamento.IgnoreQueryFilters().CountAsync(p => p.SessaoTreinamentoId == outra.Id));
        Assert.Equal(1, await db.FotosEvidenciaSessaoTreinamento.IgnoreQueryFilters().CountAsync(f => f.SessaoTreinamentoId == outra.Id));
    }

    [Fact]
    public async Task InscricaoEFotoJaExcluidasLogicamente_TambemSaem()
    {
        var db = NovoContexto(Guid.NewGuid().ToString());
        var sessao = await CriarTurmaAsync(db, ObraA);
        sessao.Participantes.First().Ativo = false;
        sessao.FotosEvidencia.First().Ativo = false;
        await db.SaveChangesAsync();

        await ExcluirAsync(db, sessao.Id);

        Assert.False(await db.ParticipantesSessaoTreinamento.IgnoreQueryFilters().AnyAsync(p => p.SessaoTreinamentoId == sessao.Id));
        Assert.False(await db.FotosEvidenciaSessaoTreinamento.IgnoreQueryFilters().AnyAsync(f => f.SessaoTreinamentoId == sessao.Id));
    }

    [Fact]
    public async Task TurmaComCertificado_EhBloqueadaENadaSomeNemEhRegistrado()
    {
        var db = NovoContexto(Guid.NewGuid().ToString());
        var sessao = await CriarTurmaAsync(db, ObraA);
        await AdicionarCertificadoAsync(db, sessao);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ExcluirAsync(db, sessao.Id));

        Assert.Contains("1 certificado emitido", ex.Message);
        Assert.True(await db.SessoesTreinamento.IgnoreQueryFilters().AnyAsync(s => s.Id == sessao.Id));
        Assert.Equal(2, await db.ParticipantesSessaoTreinamento.IgnoreQueryFilters().CountAsync(p => p.SessaoTreinamentoId == sessao.Id));
        Assert.Equal(3, await db.FotosEvidenciaSessaoTreinamento.IgnoreQueryFilters().CountAsync(f => f.SessaoTreinamentoId == sessao.Id));
        Assert.Empty(await db.TrilhaAuditoria.ToListAsync());
    }

    [Fact]
    public async Task CertificadoExcluidoLogicamente_AindaBloqueia()
    {
        var db = NovoContexto(Guid.NewGuid().ToString());
        var sessao = await CriarTurmaAsync(db, ObraA);
        var cert = await AdicionarCertificadoAsync(db, sessao);
        cert.Ativo = false;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ExcluirAsync(db, sessao.Id));

        Assert.Contains("certificado", ex.Message);
        Assert.True(await db.SessoesTreinamento.IgnoreQueryFilters().AnyAsync(s => s.Id == sessao.Id));
    }

    [Fact]
    public async Task DocumentoDaTurmaComAssinatura_Bloqueia_SemAssinaturaSaiJunto()
    {
        var db = NovoContexto(Guid.NewGuid().ToString());
        var assinada = await CriarTurmaAsync(db, ObraA);
        var documento = new DocumentoAssinatura { EntidadeTipo = "SessaoTreinamento", EntidadeId = assinada.Id };
        documento.Signatarios.Add(new DocumentoSignatario { TrabalhadorId = assinada.Participantes.First().TrabalhadorId, AssinadoEm = DateTime.UtcNow });
        db.DocumentosAssinatura.Add(documento);

        var soRastreio = await CriarTurmaAsync(db, ObraA);
        db.DocumentosAssinatura.Add(new DocumentoAssinatura { EntidadeTipo = "SessaoTreinamento", EntidadeId = soRastreio.Id });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => ExcluirAsync(db, assinada.Id));
        Assert.True(await db.SessoesTreinamento.IgnoreQueryFilters().AnyAsync(s => s.Id == assinada.Id));

        await ExcluirAsync(db, soRastreio.Id);
        Assert.False(await db.SessoesTreinamento.IgnoreQueryFilters().AnyAsync(s => s.Id == soRastreio.Id));
        Assert.False(await db.DocumentosAssinatura.IgnoreQueryFilters().AnyAsync(d => d.EntidadeId == soRastreio.Id));
    }

    [Fact]
    public async Task Exclusao_RegistraNaTrilhaORetratoSemImagens()
    {
        var db = NovoContexto(Guid.NewGuid().ToString());
        var sessao = await CriarTurmaAsync(db, ObraA);
        var trabalhadorIds = sessao.Participantes.Select(p => p.TrabalhadorId).ToList();

        await ExcluirAsync(db, sessao.Id);

        var registro = await db.TrilhaAuditoria.SingleAsync();
        Assert.Equal("SessaoTreinamento.ExcluidaDefinitivamente", registro.Acao);
        Assert.Equal(nameof(SessaoTreinamento), registro.EntidadeTipo);
        Assert.Equal(sessao.Id, registro.EntidadeId);
        var json = registro.DadosDepoisJson!;
        Assert.Contains("TRN-2026-0187", json);
        Assert.Contains("NR-35 Trabalho em Altura", json);
        Assert.Contains("\"Inscricoes\":2", json);
        Assert.Contains("\"Presencas\":1", json);
        Assert.Contains("\"FotosEvidencia\":3", json);
        foreach (var id in trabalhadorIds) Assert.Contains(id.ToString(), json);
        Assert.DoesNotContain("Evidencia\":", json.Replace("FotosEvidencia", ""));
    }

    [Fact]
    public async Task Lote_Misto_BloqueadaNaoImpedeAsOutras()
    {
        var db = NovoContexto(Guid.NewGuid().ToString());
        var livre1 = await CriarTurmaAsync(db, ObraA);
        var travada = await CriarTurmaAsync(db, ObraA);
        var livre2 = await CriarTurmaAsync(db, ObraB);
        await AdicionarCertificadoAsync(db, travada);
        var inexistente = Guid.NewGuid();

        var resultados = await ExcluirLoteAsync(db, livre1.Id, travada.Id, livre2.Id, inexistente, livre1.Id);

        Assert.Equal(4, resultados.Count); // id repetido conta uma vez
        Assert.Equal(StatusExclusaoTurma.Excluida, resultados.Single(r => r.Id == livre1.Id).Status);
        Assert.Equal(StatusExclusaoTurma.Excluida, resultados.Single(r => r.Id == livre2.Id).Status);
        var bloqueada = resultados.Single(r => r.Id == travada.Id);
        Assert.Equal(StatusExclusaoTurma.Bloqueada, bloqueada.Status);
        Assert.Contains("certificado", bloqueada.Motivo);
        Assert.Equal(StatusExclusaoTurma.NaoEncontrada, resultados.Single(r => r.Id == inexistente).Status);

        var restantes = await db.SessoesTreinamento.IgnoreQueryFilters().Select(s => s.Id).ToListAsync();
        Assert.Equal(new[] { travada.Id }, restantes);
        Assert.Equal(2, await db.TrilhaAuditoria.CountAsync());
    }

    [Fact]
    public async Task AdministradorRestritoAObra_NaoExcluiTurmaDeOutraObra()
    {
        var banco = Guid.NewGuid().ToString();
        var global = NovoContexto(banco);
        var daB = await CriarTurmaAsync(global, ObraB);
        var daA = await CriarTurmaAsync(global, ObraA);

        var restrito = new CurrentUserService();
        restrito.DefinirEscopo(false, new[] { ObraA });
        var db = NovoContexto(banco, restrito);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => ExcluirAsync(db, daB.Id));
        Assert.True(await global.SessoesTreinamento.IgnoreQueryFilters().AnyAsync(s => s.Id == daB.Id));
        Assert.Null(await new ObterPreviaExclusaoSessaoTreinamentoQueryHandler(db, new CurrentUserService())
            .Handle(new ObterPreviaExclusaoSessaoTreinamentoQuery(daB.Id), CancellationToken.None));

        await ExcluirAsync(db, daA.Id);
        Assert.False(await global.SessoesTreinamento.IgnoreQueryFilters().AnyAsync(s => s.Id == daA.Id));
    }

    // Escopo do Administrador (revisão da entrega 2): a policy global aprova Admin em qualquer vínculo; a exclusão exige Admin NA obra.
    private static async Task<(Guid UsuarioId, SstDbContext Db, SessaoTreinamento DaA, SessaoTreinamento DaB)> CenarioVinculosAsync(
        params (TipoPerfilAcesso Tipo, Guid? Obra)[] vinculos)
    {
        var banco = Guid.NewGuid().ToString();
        var global = NovoContexto(banco);
        var daA = await CriarTurmaAsync(global, ObraA);
        var daB = await CriarTurmaAsync(global, ObraB);
        var usuario = new Usuario { Nome = "Admin", Email = "a@b.c", AzureAdObjectId = Guid.NewGuid().ToString() };
        global.Usuarios.Add(usuario);
        foreach (var (tipo, obra) in vinculos)
        {
            var perfil = new PerfilAcesso { Nome = tipo.ToString() + Guid.NewGuid(), Tipo = tipo };
            global.PerfisAcesso.Add(perfil);
            global.UsuariosPerfilObra.Add(new UsuarioPerfilObra { UsuarioId = usuario.Id, PerfilAcessoId = perfil.Id, ObraId = obra });
        }
        await global.SaveChangesAsync();
        // Escopo do usuário = união das obras dos vínculos (como o EscopoPorObraMiddleware calcula).
        var atual = new CurrentUserService();
        atual.DefinirUsuarioId(usuario.Id);
        var obras = vinculos.Where(v => v.Obra.HasValue).Select(v => v.Obra!.Value).ToList();
        atual.DefinirEscopo(vinculos.Any(v => v.Obra is null), obras);
        return (usuario.Id, NovoContexto(banco, atual), daA, daB);
    }

    private static Task<List<ResultadoExclusaoTurma>> LoteComoAsync(SstDbContext db, Guid usuarioId, params Guid[] ids)
        => new ExcluirSessoesTreinamentoLoteCommandHandler(db, new AuditoriaGravando(db), ComUsuario(usuarioId, db))
            .Handle(new ExcluirSessoesTreinamentoLoteCommand(ids), CancellationToken.None);

    private static CurrentUserService ComUsuario(Guid usuarioId, SstDbContext db)
    {
        var u = new CurrentUserService();
        u.DefinirUsuarioId(usuarioId);
        u.DefinirEscopo(db.EscopoObraGlobal, db.ObrasNoEscopo);
        return u;
    }

    [Fact]
    public async Task AdminEmAComTecnicoEmB_NaoExcluiTurmaDeB_NemNoLoteNemNaPrevia()
    {
        var (uid, db, daA, daB) = await CenarioVinculosAsync((TipoPerfilAcesso.Administrador, ObraA), (TipoPerfilAcesso.TecnicoSeguranca, ObraB));

        var r = await LoteComoAsync(db, uid, daB.Id);
        Assert.Equal(StatusExclusaoTurma.NaoEncontrada, r.Single().Status);
        Assert.True(await db.SessoesTreinamento.IgnoreQueryFilters().AnyAsync(s => s.Id == daB.Id));

        var handler = new ExcluirSessaoTreinamentoCommandHandler(db, new AuditoriaGravando(db), ComUsuario(uid, db));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(new ExcluirSessaoTreinamentoCommand(daB.Id), CancellationToken.None));

        Assert.Null(await new ObterPreviaExclusaoSessaoTreinamentoQueryHandler(db, ComUsuario(uid, db))
            .Handle(new ObterPreviaExclusaoSessaoTreinamentoQuery(daB.Id), CancellationToken.None));
        Assert.Empty(await db.TrilhaAuditoria.ToListAsync());
    }

    [Fact]
    public async Task AdminEmA_ExcluiTurmaDeA()
    {
        var (uid, db, daA, _) = await CenarioVinculosAsync((TipoPerfilAcesso.Administrador, ObraA), (TipoPerfilAcesso.TecnicoSeguranca, ObraB));

        var r = await LoteComoAsync(db, uid, daA.Id);

        Assert.Equal(StatusExclusaoTurma.Excluida, r.Single().Status);
        Assert.False(await db.SessoesTreinamento.IgnoreQueryFilters().AnyAsync(s => s.Id == daA.Id));
    }

    [Fact]
    public async Task AdminGlobal_ExcluiTurmaDeQualquerObra()
    {
        var (uid, db, daA, daB) = await CenarioVinculosAsync((TipoPerfilAcesso.Administrador, null));

        var r = await LoteComoAsync(db, uid, daA.Id, daB.Id);

        Assert.All(r, x => Assert.Equal(StatusExclusaoTurma.Excluida, x.Status));
    }

    [Fact]
    public async Task Previa_InformaListaDePresencaGerada()
    {
        var db = NovoContexto(Guid.NewGuid().ToString());
        var sessao = await CriarTurmaAsync(db, ObraA);
        var handler = new ObterPreviaExclusaoSessaoTreinamentoQueryHandler(db, new CurrentUserService());
        Assert.False((await handler.Handle(new ObterPreviaExclusaoSessaoTreinamentoQuery(sessao.Id), CancellationToken.None))!.ListaPresencaGerada);

        db.DocumentosAssinatura.Add(new DocumentoAssinatura { EntidadeTipo = "SessaoTreinamento", EntidadeId = sessao.Id });
        await db.SaveChangesAsync();
        Assert.True((await handler.Handle(new ObterPreviaExclusaoSessaoTreinamentoQuery(sessao.Id), CancellationToken.None))!.ListaPresencaGerada);
    }

    [Fact]
    public async Task Previa_ContaOQueVaiJuntoEDizSePode()
    {
        var db = NovoContexto(Guid.NewGuid().ToString());
        var livre = await CriarTurmaAsync(db, ObraA);
        var travada = await CriarTurmaAsync(db, ObraA);
        await AdicionarCertificadoAsync(db, travada);
        var handler = new ObterPreviaExclusaoSessaoTreinamentoQueryHandler(db, new CurrentUserService());

        var p1 = await handler.Handle(new ObterPreviaExclusaoSessaoTreinamentoQuery(livre.Id), CancellationToken.None);
        Assert.NotNull(p1);
        Assert.Equal((2, 1, 3, 0), (p1!.Inscricoes, p1.Presencas, p1.FotosEvidencia, p1.CertificadosEmitidos));
        Assert.True(p1.PodeExcluir);
        Assert.Null(p1.Motivo);

        var p2 = await handler.Handle(new ObterPreviaExclusaoSessaoTreinamentoQuery(travada.Id), CancellationToken.None);
        Assert.False(p2!.PodeExcluir);
        Assert.Equal(1, p2.CertificadosEmitidos);
        Assert.Contains("certificado", p2.Motivo);

        // Prévia não apaga nada.
        Assert.Equal(2, await db.SessoesTreinamento.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task ListaInformaCertificadosPorTurma()
    {
        var db = NovoContexto(Guid.NewGuid().ToString());
        var livre = await CriarTurmaAsync(db, ObraA);
        var travada = await CriarTurmaAsync(db, ObraA);
        var cert = await AdicionarCertificadoAsync(db, travada);
        cert.Ativo = false; // excluído logicamente continua contando
        await db.SaveChangesAsync();

        var lista = await new ListarSessoesTreinamentoQueryHandler(db).Handle(new ListarSessoesTreinamentoQuery(), CancellationToken.None);

        Assert.Equal(0, lista.Single(s => s.Id == livre.Id).TotalCertificadosEmitidos);
        Assert.Equal(1, lista.Single(s => s.Id == travada.Id).TotalCertificadosEmitidos);
    }

    [Fact]
    public async Task OutrasExclusoes_ContinuamLogicas()
    {
        var db = NovoContexto(Guid.NewGuid().ToString());
        var sessao = await CriarTurmaAsync(db, ObraA);
        var foto = sessao.FotosEvidencia.First();
        var participante = sessao.Participantes.First();

        // Mesmo caminho do Remove usado pelos demais comandos (ex.: RemoverFotoEvidenciaSessaoTreinamentoCommand).
        db.FotosEvidenciaSessaoTreinamento.Remove(foto);
        db.ParticipantesSessaoTreinamento.Remove(participante);
        await db.SaveChangesAsync();

        var fotoNoBanco = await db.FotosEvidenciaSessaoTreinamento.IgnoreQueryFilters().SingleAsync(f => f.Id == foto.Id);
        var participanteNoBanco = await db.ParticipantesSessaoTreinamento.IgnoreQueryFilters().SingleAsync(p => p.Id == participante.Id);
        Assert.False(fotoNoBanco.Ativo);
        Assert.False(participanteNoBanco.Ativo);
    }

    [Fact]
    public async Task MarcacaoFisicaNaoVazaParaOProximoSalvamento()
    {
        var db = NovoContexto(Guid.NewGuid().ToString());
        var sessao = await CriarTurmaAsync(db, ObraA);
        var foto = sessao.FotosEvidencia.First();

        db.MarcarExclusaoFisica(foto);
        await db.SaveChangesAsync();
        Assert.False(await db.FotosEvidenciaSessaoTreinamento.IgnoreQueryFilters().AnyAsync(f => f.Id == foto.Id));

        // Outra foto removida depois, pelo caminho normal, continua sendo exclusão lógica.
        var outra = sessao.FotosEvidencia.Last();
        db.FotosEvidenciaSessaoTreinamento.Remove(outra);
        await db.SaveChangesAsync();
        Assert.False((await db.FotosEvidenciaSessaoTreinamento.IgnoreQueryFilters().SingleAsync(f => f.Id == outra.Id)).Ativo);
    }
}
