using PainelComercial.Api.Comum;
using PainelComercial.Api.Juros;

namespace PainelComercial.Tests;

public class CalculadoraJurosTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 6);

    [Fact]
    public void Antes_do_vencimento_nao_ha_juros()
    {
        var calculo = CalculadoraJuros.Calcular(1000m, Hoje.AddDays(5), Hoje);

        Assert.Equal(0, calculo.DiasAtraso);
        Assert.Equal(0m, calculo.ValorJuros);
        Assert.Equal(1000m, calculo.ValorAtualizado);
    }

    [Fact]
    public void No_dia_do_vencimento_nao_ha_juros()
    {
        var calculo = CalculadoraJuros.Calcular(1000m, Hoje, Hoje);

        Assert.Equal(0, calculo.DiasAtraso);
        Assert.Equal(0m, calculo.ValorJuros);
    }

    public static TheoryData<int, decimal, decimal> Atrasos => new()
    {
        { 1, 2.5m, 25.00m },
        { 10, 25m, 250.00m },
        { 40, 100m, 1000.00m },
    };

    [Theory]
    [MemberData(nameof(Atrasos))]
    public void Cobra_2_5_por_cento_por_dia_de_atraso(int diasAtraso, decimal percentualTotal, decimal juros)
    {
        var calculo = CalculadoraJuros.Calcular(1000m, Hoje.AddDays(-diasAtraso), Hoje);

        Assert.Equal(diasAtraso, calculo.DiasAtraso);
        Assert.Equal(2.5m, calculo.PercentualDiario);
        Assert.Equal(percentualTotal, calculo.PercentualTotal);
        Assert.Equal(juros, calculo.ValorJuros);
        Assert.Equal(1000m + juros, calculo.ValorAtualizado);
    }

    [Fact]
    public void Arredonda_os_juros_para_centavos()
    {
        // 333,33 × 7,5% = 24,99975 → R$ 25,00
        var calculo = CalculadoraJuros.Calcular(333.33m, Hoje.AddDays(-3), Hoje);

        Assert.Equal(25.00m, calculo.ValorJuros);
        Assert.Equal(358.33m, calculo.ValorAtualizado);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Valor_deve_ser_maior_que_zero(int valor)
    {
        Assert.Throws<RegraNegocioException>(() => CalculadoraJuros.Calcular(valor, Hoje, Hoje));
    }
}
