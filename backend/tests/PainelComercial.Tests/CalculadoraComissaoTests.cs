using PainelComercial.Api.Comissoes;
using PainelComercial.Api.Comum;

namespace PainelComercial.Tests;

public class CalculadoraComissaoTests
{
    public static TheoryData<decimal, decimal> Faixas => new()
    {
        { 0.01m, 0m },
        { 99.99m, 0m },
        { 100.00m, 1m },
        { 499.99m, 1m },
        { 500.00m, 5m },
        { 2100.40m, 5m },
    };

    [Theory]
    [MemberData(nameof(Faixas))]
    public void PercentualPara_aplica_a_faixa_do_valor_da_venda(decimal valorVenda, decimal percentualEsperado)
    {
        Assert.Equal(percentualEsperado, CalculadoraComissao.PercentualPara(valorVenda));
    }

    [Fact]
    public void Calcular_agrupa_por_vendedor_na_ordem_em_que_aparecem()
    {
        var relatorio = CalculadoraComissao.Calcular(
        [
            new Venda("Ana", 50m),     // sem comissão
            new Venda("Bruno", 200m),  // 1% = 2,00
            new Venda("Ana", 1000m),   // 5% = 50,00
            new Venda("Ana", 300m),    // 1% = 3,00
        ]);

        Assert.Collection(relatorio.Vendedores,
            ana =>
            {
                Assert.Equal("Ana", ana.Vendedor);
                Assert.Equal(3, ana.QuantidadeVendas);
                Assert.Equal(1350m, ana.TotalVendido);
                Assert.Equal(53m, ana.TotalComissao);
                Assert.Equal(new[] { 0m, 5m, 1m }, ana.Vendas.Select(v => v.PercentualComissao));
            },
            bruno =>
            {
                Assert.Equal("Bruno", bruno.Vendedor);
                Assert.Equal(2m, bruno.TotalComissao);
            });

        Assert.Equal(1550m, relatorio.TotalVendido);
        Assert.Equal(55m, relatorio.TotalComissao);
    }

    [Fact]
    public void Calcular_arredonda_somente_o_total_do_vendedor()
    {
        // 5% de 1.200,50 = 60,025 e 5% de 950,75 = 47,5375 → 107,5625 → R$ 107,56.
        // Arredondando venda a venda daria 60,03 + 47,54 = R$ 107,57.
        var relatorio = CalculadoraComissao.Calcular([new Venda("João", 1200.50m), new Venda("João", 950.75m)]);

        var joao = Assert.Single(relatorio.Vendedores);
        Assert.Equal(new[] { 60.025m, 47.5375m }, joao.Vendas.Select(v => v.Comissao));
        Assert.Equal(107.56m, joao.TotalComissao);
    }

    [Fact]
    public void Calcular_ignora_espacos_e_maiusculas_no_nome_do_vendedor()
    {
        var relatorio = CalculadoraComissao.Calcular([new Venda("Ana Lima", 600m), new Venda(" ana lima ", 600m)]);

        var ana = Assert.Single(relatorio.Vendedores);
        Assert.Equal("Ana Lima", ana.Vendedor);
        Assert.Equal(60m, ana.TotalComissao);
    }

    [Theory]
    [InlineData("João Silva", 10, "10754.70", "495.68")]
    [InlineData("Maria Souza", 9, "9874.30", "465.95")]
    [InlineData("Carlos Oliveira", 8, "7928.35", "379.37")]
    [InlineData("Ana Lima", 9, "8763.95", "404.98")]
    public void Calcular_com_as_vendas_do_arquivo(string vendedor, int quantidade, string totalVendido, string totalComissao)
    {
        var vendas = ArquivosDados.LerVendas(Path.Combine(AppContext.BaseDirectory, "Data"));

        var resultado = CalculadoraComissao.Calcular(vendas).Vendedores.Single(v => v.Vendedor == vendedor);

        Assert.Equal(quantidade, resultado.QuantidadeVendas);
        Assert.Equal(decimal.Parse(totalVendido, System.Globalization.CultureInfo.InvariantCulture), resultado.TotalVendido);
        Assert.Equal(decimal.Parse(totalComissao, System.Globalization.CultureInfo.InvariantCulture), resultado.TotalComissao);
    }
}
