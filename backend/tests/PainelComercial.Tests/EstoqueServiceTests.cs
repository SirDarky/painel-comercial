using PainelComercial.Api.Comum;
using PainelComercial.Api.Estoque;
using Microsoft.EntityFrameworkCore;

namespace PainelComercial.Tests;

public sealed class EstoqueServiceTests : IDisposable
{
    private readonly BancoDeTeste _banco = new();

    public EstoqueServiceTests()
    {
        using var db = _banco.CriarContexto();
        db.Database.Migrate();
        db.Produtos.AddRange(
            new Produto { CodigoProduto = 101, DescricaoProduto = "Caneta Azul", Estoque = 150 },
            new Produto { CodigoProduto = 102, DescricaoProduto = "Caderno Universitário", Estoque = 75 });
        db.SaveChanges();
    }

    public void Dispose() => _banco.Dispose();

    /// <summary>Cada operação usa um contexto (e uma conexão) novo, como cada requisição da API.</summary>
    private async Task<T> ComServico<T>(Func<EstoqueService, Task<T>> operacao)
    {
        await using var db = _banco.CriarContexto();
        return await operacao(new EstoqueService(db, RelogioFixo.Em06Out2026));
    }

    private Task<MovimentacaoResponse> Movimentar(int codigoProduto, TipoMovimentacao tipo, int quantidade, string descricao) =>
        ComServico(estoque => estoque.MovimentarAsync(codigoProduto, tipo, quantidade, descricao));

    private async Task<int> EstoqueAtual(int codigoProduto) =>
        (await ComServico(estoque => estoque.ListarProdutosAsync())).Single(p => p.CodigoProduto == codigoProduto).Estoque;

    [Fact]
    public async Task Entrada_soma_ao_estoque_e_devolve_o_estoque_final()
    {
        var movimentacao = await Movimentar(101, TipoMovimentacao.Entrada, 50, "Compra de fornecedor");

        Assert.Equal(150, movimentacao.EstoqueAnterior);
        Assert.Equal(200, movimentacao.EstoqueFinal);
        Assert.Equal(200, await EstoqueAtual(101));
    }

    [Fact]
    public async Task Saida_subtrai_do_estoque_e_pode_zerar_o_saldo()
    {
        var movimentacao = await Movimentar(102, TipoMovimentacao.Saida, 75, "Venda");

        Assert.Equal(0, movimentacao.EstoqueFinal);
        Assert.Equal(0, await EstoqueAtual(102));
    }

    [Fact]
    public async Task Saida_maior_que_o_estoque_e_rejeitada_sem_alterar_o_saldo()
    {
        var erro = await Assert.ThrowsAsync<RegraNegocioException>(
            () => Movimentar(102, TipoMovimentacao.Saida, 76, "Venda"));

        Assert.Equal("Estoque insuficiente de \"Caderno Universitário\": disponível 75, solicitado 76.", erro.Message);
        Assert.Equal(75, await EstoqueAtual(102));
        Assert.Empty(await ComServico(estoque => estoque.ListarMovimentacoesAsync()));
    }

    [Fact]
    public async Task Grava_a_movimentacao_no_banco()
    {
        var lancada = await Movimentar(101, TipoMovimentacao.Saida, 10, "  Venda balcão  ");

        var gravada = await ComServico(estoque => estoque.ObterMovimentacaoAsync(lancada.Id));

        Assert.Equal(new MovimentacaoResponse(
            Id: 1,
            CodigoProduto: 101,
            DescricaoProduto: "Caneta Azul",
            Tipo: TipoMovimentacao.Saida,
            Quantidade: 10,
            Descricao: "Venda balcão",
            DataHora: RelogioFixo.Em06Out2026.GetLocalNow(),
            EstoqueAnterior: 150,
            EstoqueFinal: 140), gravada);
        Assert.Equal(lancada, gravada);
    }

    [Fact]
    public async Task Cada_movimentacao_recebe_um_numero_unico_e_sequencial()
    {
        await Movimentar(101, TipoMovimentacao.Entrada, 10, "Compra");
        await Movimentar(102, TipoMovimentacao.Saida, 5, "Venda");
        await Movimentar(101, TipoMovimentacao.Saida, 1, "Avaria");

        var todas = await ComServico(estoque => estoque.ListarMovimentacoesAsync());
        var doProduto101 = await ComServico(estoque => estoque.ListarMovimentacoesAsync(codigoProduto: 101));
        Assert.Equal(new long[] { 3, 2, 1 }, todas.Select(m => m.Id));
        Assert.Equal(new long[] { 3, 1 }, doProduto101.Select(m => m.Id));
    }

    [Fact]
    public async Task Entradas_simultaneas_nao_perdem_atualizacoes()
    {
        await Parallel.ForEachAsync(Enumerable.Range(0, 100), async (_, _) =>
            await Movimentar(101, TipoMovimentacao.Entrada, 1, "Entrada"));

        Assert.Equal(250, await EstoqueAtual(101));
        var movimentacoes = await ComServico(estoque => estoque.ListarMovimentacoesAsync());
        Assert.Equal(100, movimentacoes.Select(m => m.Id).Distinct().Count());
    }

    [Fact]
    public async Task Saidas_simultaneas_nunca_deixam_o_estoque_negativo()
    {
        var recusadas = 0;

        // 100 saídas de 1 unidade disputando um estoque de 75.
        await Parallel.ForEachAsync(Enumerable.Range(0, 100), async (_, _) =>
        {
            try
            {
                await Movimentar(102, TipoMovimentacao.Saida, 1, "Venda");
            }
            catch (RegraNegocioException)
            {
                Interlocked.Increment(ref recusadas);
            }
        });

        Assert.Equal(25, recusadas);
        Assert.Equal(0, await EstoqueAtual(102));
        Assert.Equal(75, (await ComServico(estoque => estoque.ListarMovimentacoesAsync())).Count);
    }

    [Fact]
    public async Task Repetir_a_mesma_chave_de_idempotencia_nao_lanca_de_novo()
    {
        var primeira = await ComServico(e => e.MovimentarAsync(101, TipoMovimentacao.Saida, 10, "Venda", "chave-1"));
        var repetida = await ComServico(e => e.MovimentarAsync(101, TipoMovimentacao.Saida, 10, "Venda", "chave-1"));

        Assert.Equal(primeira, repetida);
        Assert.Equal(140, await EstoqueAtual(101));
        Assert.Single(await ComServico(estoque => estoque.ListarMovimentacoesAsync()));
    }

    [Fact]
    public async Task Repeticoes_simultaneas_com_a_mesma_chave_geram_uma_unica_movimentacao()
    {
        await Parallel.ForEachAsync(Enumerable.Range(0, 20), async (_, _) =>
            await ComServico(e => e.MovimentarAsync(102, TipoMovimentacao.Saida, 5, "Venda", "duplo-clique")));

        Assert.Equal(70, await EstoqueAtual(102));
        Assert.Single(await ComServico(estoque => estoque.ListarMovimentacoesAsync()));
    }

    [Fact]
    public async Task Mesma_chave_com_outros_dados_gera_conflito()
    {
        await ComServico(e => e.MovimentarAsync(101, TipoMovimentacao.Saida, 10, "Venda", "chave-1"));

        await Assert.ThrowsAsync<ConflitoException>(
            () => ComServico(e => e.MovimentarAsync(101, TipoMovimentacao.Saida, 11, "Venda", "chave-1")));
        Assert.Equal(140, await EstoqueAtual(101));
    }

    [Fact]
    public async Task Produto_inexistente_gera_recurso_nao_encontrado()
    {
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(
            () => Movimentar(999, TipoMovimentacao.Entrada, 1, "Compra"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Quantidade_deve_ser_maior_que_zero(int quantidade)
    {
        await Assert.ThrowsAsync<RegraNegocioException>(
            () => Movimentar(101, TipoMovimentacao.Entrada, quantidade, "Compra"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Descricao_e_obrigatoria(string descricao)
    {
        await Assert.ThrowsAsync<RegraNegocioException>(
            () => Movimentar(101, TipoMovimentacao.Entrada, 1, descricao));
    }
}
