using System.Text.Json;
using PainelComercial.Api.Comissoes;
using PainelComercial.Api.Estoque;

namespace PainelComercial.Api.Comum;

/// <summary>Lê os arquivos JSON com os dados iniciais (pasta <c>Data</c>).</summary>
public static class ArquivosDados
{
    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<Venda> LerVendas(string pastaDados) =>
        Ler<ListaVendas>(Path.Combine(pastaDados, "vendas.json")).Vendas ?? [];

    public static IReadOnlyList<Produto> LerEstoque(string pastaDados) =>
        Ler<ArquivoEstoque>(Path.Combine(pastaDados, "estoque.json")).Estoque ?? [];

    private static T Ler<T>(string caminho)
    {
        using var arquivo = File.OpenRead(caminho);
        return JsonSerializer.Deserialize<T>(arquivo, Opcoes)
            ?? throw new InvalidDataException($"O arquivo de dados '{caminho}' está vazio.");
    }

    private sealed record ArquivoEstoque(IReadOnlyList<Produto>? Estoque);
}
