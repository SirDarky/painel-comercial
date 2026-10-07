namespace PainelComercial.Api.Comissoes;

public sealed record ComissaoVenda(decimal Valor, decimal PercentualComissao, decimal Comissao);

public sealed record ComissaoVendedor(
    string Vendedor,
    int QuantidadeVendas,
    decimal TotalVendido,
    decimal TotalComissao,
    IReadOnlyList<ComissaoVenda> Vendas);

public sealed record RelatorioComissoes(
    IReadOnlyList<ComissaoVendedor> Vendedores,
    decimal TotalVendido,
    decimal TotalComissao);

public static class CalculadoraComissao
{
    /// <summary>Percentual de comissão de uma venda, conforme as faixas de valor.</summary>
    public static decimal PercentualPara(decimal valorVenda) => valorVenda switch
    {
        < 100m => 0m, // abaixo de R$ 100,00: não gera comissão
        < 500m => 1m, // abaixo de R$ 500,00: 1%
        _ => 5m       // a partir de R$ 500,00: 5%
    };

    /// <summary>
    /// Agrupa as vendas por vendedor (na ordem em que aparecem) e soma as comissões.
    /// A comissão de cada venda é mantida com precisão total; só o total de cada
    /// vendedor é arredondado para centavos, evitando acúmulo de erro de arredondamento.
    /// </summary>
    public static RelatorioComissoes Calcular(IEnumerable<Venda> vendas)
    {
        var vendedores = vendas
            .GroupBy(venda => venda.Vendedor.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(grupo =>
            {
                var comissoes = grupo
                    .Select(venda =>
                    {
                        var percentual = PercentualPara(venda.Valor);
                        return new ComissaoVenda(venda.Valor, percentual, venda.Valor * percentual / 100m);
                    })
                    .ToList();

                return new ComissaoVendedor(
                    grupo.Key,
                    comissoes.Count,
                    comissoes.Sum(c => c.Valor),
                    ArredondarCentavos(comissoes.Sum(c => c.Comissao)),
                    comissoes);
            })
            .ToList();

        return new RelatorioComissoes(
            vendedores,
            vendedores.Sum(v => v.TotalVendido),
            vendedores.Sum(v => v.TotalComissao));
    }

    private static decimal ArredondarCentavos(decimal valor) =>
        Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}
