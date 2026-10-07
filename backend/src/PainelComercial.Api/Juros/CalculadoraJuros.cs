using PainelComercial.Api.Comum;

namespace PainelComercial.Api.Juros;

public sealed record CalculoJuros(
    decimal Valor,
    DateOnly DataVencimento,
    DateOnly DataCalculo,
    int DiasAtraso,
    decimal PercentualDiario,
    decimal PercentualTotal,
    decimal ValorJuros,
    decimal ValorAtualizado);

public static class CalculadoraJuros
{
    /// <summary>Multa de 2,5% por dia de atraso.</summary>
    public const decimal PercentualDiario = 2.5m;

    /// <summary>
    /// Juros simples sobre o valor original: valor × 2,5% × dias em atraso.
    /// Se a data de cálculo não passou do vencimento, não há juros.
    /// </summary>
    public static CalculoJuros Calcular(decimal valor, DateOnly dataVencimento, DateOnly dataCalculo)
    {
        if (valor <= 0)
            throw new RegraNegocioException("O valor deve ser maior que zero.");

        var diasAtraso = Math.Max(0, dataCalculo.DayNumber - dataVencimento.DayNumber);
        var percentualTotal = PercentualDiario * diasAtraso;
        var juros = Math.Round(valor * percentualTotal / 100m, 2, MidpointRounding.AwayFromZero);

        return new CalculoJuros(
            valor,
            dataVencimento,
            dataCalculo,
            diasAtraso,
            PercentualDiario,
            percentualTotal,
            juros,
            valor + juros);
    }
}
