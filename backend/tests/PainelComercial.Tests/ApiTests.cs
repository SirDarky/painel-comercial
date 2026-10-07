using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PainelComercial.Api.Comissoes;
using PainelComercial.Api.Estoque;
using PainelComercial.Api.Juros;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace PainelComercial.Tests;

/// <summary>Testes de ponta a ponta da API, com o relógio fixado em 06/10/2026.</summary>
public sealed class ApiTests : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // Cada teste tem o próprio banco SQLite, criado do zero pelas migrations e pelo estoque.json.
    private readonly BancoDeTeste _banco = new();
    private readonly WebApplicationFactory<Program> _api;
    private readonly HttpClient _cliente;

    public ApiTests()
    {
        _api = CriarApi();
        _cliente = _api.CreateClient();
    }

    private WebApplicationFactory<Program> CriarApi(params (string Chave, string Valor)[] configuracoes) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.UseSetting("ConnectionStrings:Estoque", _banco.ConnectionString);
            foreach (var (chave, valor) in configuracoes)
                host.UseSetting(chave, valor);
            host.ConfigureTestServices(servicos => servicos.AddSingleton<TimeProvider>(RelogioFixo.Em06Out2026));
        });

    public void Dispose()
    {
        _api.Dispose();
        _banco.Dispose();
    }

    [Fact]
    public async Task Comissoes_sao_calculadas_a_partir_do_vendas_json()
    {
        var relatorio = await _cliente.GetFromJsonAsync<RelatorioComissoes>("/api/comissoes", Json);

        Assert.NotNull(relatorio);
        Assert.Equal(
            new[] { ("João Silva", 495.68m), ("Maria Souza", 465.95m), ("Carlos Oliveira", 379.37m), ("Ana Lima", 404.98m) },
            relatorio.Vendedores.Select(v => (v.Vendedor, v.TotalComissao)));
        Assert.Equal(37321.30m, relatorio.TotalVendido);
        Assert.Equal(1745.98m, relatorio.TotalComissao);
    }

    [Fact]
    public async Task Calculo_de_comissao_usa_as_vendas_enviadas_no_corpo()
    {
        var resposta = await _cliente.PostAsJsonAsync("/api/comissoes/calculo",
            new { vendas = new[] { new { vendedor = "Teste", valor = 99.99m }, new { vendedor = "Teste", valor = 500m } } });

        resposta.EnsureSuccessStatusCode();
        var relatorio = await resposta.Content.ReadFromJsonAsync<RelatorioComissoes>(Json);
        var teste = Assert.Single(relatorio!.Vendedores);
        Assert.Equal("Teste", teste.Vendedor);
        Assert.Equal(25m, teste.TotalComissao);
    }

    [Fact]
    public async Task Calculo_de_comissao_rejeita_vendas_invalidas()
    {
        var resposta = await _cliente.PostAsJsonAsync("/api/comissoes/calculo",
            new { vendas = new[] { new { vendedor = "", valor = -10m } } });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var problema = await resposta.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal("Informe o nome do vendedor.", Assert.Single(problema!.Errors["Vendas[0].Vendedor"]));
        Assert.Contains("O valor da venda deve estar entre", problema.Errors["Vendas[0].Valor"].Single());
    }

    [Fact]
    public async Task Movimentacao_devolve_201_com_o_estoque_final_do_produto()
    {
        var resposta = await _cliente.PostAsJsonAsync("/api/estoque/movimentacoes",
            new { codigoProduto = 101, tipo = "Entrada", quantidade = 50, descricao = "Compra de fornecedor" });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var movimentacao = await resposta.Content.ReadFromJsonAsync<MovimentacaoResponse>(Json);
        Assert.Equal(1, movimentacao!.Id);
        Assert.Equal(TipoMovimentacao.Entrada, movimentacao.Tipo);
        Assert.Equal("Caneta Azul", movimentacao.DescricaoProduto);
        Assert.Equal(150, movimentacao.EstoqueAnterior);
        Assert.Equal(200, movimentacao.EstoqueFinal);
        Assert.Equal("/api/estoque/movimentacoes/1", resposta.Headers.Location?.AbsolutePath);

        var produtos = await _cliente.GetFromJsonAsync<List<Produto>>("/api/estoque/produtos", Json);
        Assert.Equal(200, produtos!.Single(p => p.CodigoProduto == 101).Estoque);
    }

    [Fact]
    public async Task Movimentacoes_continuam_gravadas_depois_de_reiniciar_a_api()
    {
        await _cliente.PostAsJsonAsync("/api/estoque/movimentacoes",
            new { codigoProduto = 103, tipo = "Saida", quantidade = 20, descricao = "Venda ao cliente" });
        await _api.DisposeAsync();

        await using var apiReiniciada = CriarApi();
        var cliente = apiReiniciada.CreateClient();

        var movimentacoes = await cliente.GetFromJsonAsync<List<MovimentacaoResponse>>("/api/estoque/movimentacoes", Json);
        var produtos = await cliente.GetFromJsonAsync<List<Produto>>("/api/estoque/produtos", Json);
        Assert.Equal("Venda ao cliente", Assert.Single(movimentacoes!).Descricao);
        Assert.Equal(180, produtos!.Single(p => p.CodigoProduto == 103).Estoque);
    }

    [Fact]
    public async Task Saida_maior_que_o_estoque_devolve_422()
    {
        var resposta = await _cliente.PostAsJsonAsync("/api/estoque/movimentacoes",
            new { codigoProduto = 105, tipo = "Saida", quantidade = 91, descricao = "Venda" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        var problema = await resposta.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Estoque insuficiente de \"Marcador de Texto Amarelo\": disponível 90, solicitado 91.", problema!.Detail);
    }

    [Fact]
    public async Task Movimentacao_de_produto_inexistente_devolve_404()
    {
        var resposta = await _cliente.PostAsJsonAsync("/api/estoque/movimentacoes",
            new { codigoProduto = 999, tipo = "Entrada", quantidade = 1, descricao = "Compra" });

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Theory]
    [InlineData("""{ "codigoProduto": 101, "tipo": "Transferencia", "quantidade": 1, "descricao": "x" }""")]
    [InlineData("""{ "codigoProduto": 101, "tipo": 1, "quantidade": 1, "descricao": "x" }""")]
    [InlineData("""{ "codigoProduto": 101, "tipo": "Entrada", "quantidade": 0, "descricao": "x" }""")]
    [InlineData("""{ "codigoProduto": 101, "tipo": "Entrada", "quantidade": 1 }""")]
    [InlineData("""{ "codigoProduto": 101, "tipo": "Entrada", "quantidade": 1000001, "descricao": "x" }""")]
    public async Task Movimentacao_invalida_devolve_400(string corpo)
    {
        var resposta = await _cliente.PostAsync("/api/estoque/movimentacoes",
            new StringContent(corpo, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    public static TheoryData<string, string, string, string> ErrosDeFormato => new()
    {
        {
            "/api/estoque/movimentacoes",
            """{ "codigoProduto": 101, "tipo": "Entrada", "quantidade": 1.5, "descricao": "x" }""",
            "quantidade",
            "Valor em formato inválido para o campo 'quantidade'."
        },
        {
            "/api/estoque/movimentacoes",
            """{ "codigoProduto": 101, "tipo": "Transferencia", "quantidade": 1, "descricao": "x" }""",
            "tipo",
            "Valor em formato inválido para o campo 'tipo'."
        },
        { "/api/estoque/movimentacoes", "{ isto não é json", "corpo", "O corpo da requisição não é um JSON válido." },
        { "/api/estoque/movimentacoes", "", "corpo", "Envie o corpo da requisição em JSON." },
    };

    [Theory]
    [MemberData(nameof(ErrosDeFormato))]
    public async Task Erros_de_formato_vem_em_portugues_sem_detalhes_internos(
        string url, string corpo, string campo, string mensagem)
    {
        var resposta = await _cliente.PostAsync(url, new StringContent(corpo, Encoding.UTF8, "application/json"));
        var texto = await resposta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var problema = JsonSerializer.Deserialize<ValidationProblemDetails>(texto, Json)!;
        Assert.Equal("Dados inválidos.", problema.Title);
        Assert.Equal(mensagem, Assert.Single(problema.Errors[campo]));
        Assert.DoesNotContain("PainelComercial", texto);
        Assert.DoesNotContain("System.", texto);
    }

    [Fact]
    public async Task Calculo_de_comissao_limita_o_tamanho_dos_dados()
    {
        var nomeLongo = await _cliente.PostAsJsonAsync("/api/comissoes/calculo",
            new { vendas = new[] { new { vendedor = new string('A', 101), valor = 600m } } });
        var listaGrande = await _cliente.PostAsJsonAsync("/api/comissoes/calculo",
            new { vendas = Enumerable.Range(0, 10_001).Select(i => new { vendedor = "V", valor = 600m }) });

        Assert.Equal(HttpStatusCode.BadRequest, nomeLongo.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, listaGrande.StatusCode);
        var problema = await listaGrande.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal("Envie no máximo 10.000 vendas por cálculo.", Assert.Single(problema!.Errors["Vendas"]));
    }

    [Fact]
    public async Task Mesma_idempotency_key_nao_lanca_a_movimentacao_duas_vezes()
    {
        var corpo = new { codigoProduto = 104, tipo = "Saida", quantidade = 20, descricao = "Venda ao cliente" };

        var primeira = await PostarComChave(corpo, "chave-123");
        var repetida = await PostarComChave(corpo, "chave-123");
        var outroCorpo = await PostarComChave(corpo with { quantidade = 21 }, "chave-123");

        Assert.Equal(HttpStatusCode.Created, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.Created, repetida.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, outroCorpo.StatusCode);
        var (a, b) = (await primeira.Content.ReadFromJsonAsync<MovimentacaoResponse>(Json),
            await repetida.Content.ReadFromJsonAsync<MovimentacaoResponse>(Json));
        Assert.Equal(a, b);

        var produtos = await _cliente.GetFromJsonAsync<List<Produto>>("/api/estoque/produtos", Json);
        Assert.Equal(300, produtos!.Single(p => p.CodigoProduto == 104).Estoque);
    }

    private Task<HttpResponseMessage> PostarComChave(object corpo, string chave)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/estoque/movimentacoes")
        {
            Content = JsonContent.Create(corpo)
        };
        requisicao.Headers.Add("Idempotency-Key", chave);
        return _cliente.SendAsync(requisicao);
    }

    [Fact]
    public async Task Rotas_de_escrita_tem_limite_de_requisicoes_por_minuto()
    {
        await using var api = CriarApi(("LimitesDeRequisicao:EscritasPorMinuto", "2"));
        var cliente = api.CreateClient();
        var corpo = new { vendas = new[] { new { vendedor = "Ana", valor = 600m } } };

        var respostas = new List<HttpResponseMessage>();
        for (var i = 0; i < 3; i++)
            respostas.Add(await cliente.PostAsJsonAsync("/api/comissoes/calculo", corpo));

        Assert.Equal(
            new[] { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests },
            respostas.Select(r => r.StatusCode));
        var bloqueada = respostas[2];
        Assert.True(bloqueada.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        var problema = await bloqueada.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Limite de requisições atingido. Aguarde um instante e tente novamente.", problema!.Detail);

        // As leituras seguem o limite geral, que é maior.
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/estoque/produtos")).StatusCode);
    }

    [Fact]
    public async Task Todas_as_rotas_tem_limite_geral_de_requisicoes()
    {
        await using var api = CriarApi(("LimitesDeRequisicao:RequisicoesPorMinuto", "3"));
        var cliente = api.CreateClient();

        var status = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
            status.Add((await cliente.GetAsync("/api/estoque/produtos")).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, status[^1]);
        Assert.All(status[..^1], s => Assert.Equal(HttpStatusCode.OK, s));
    }

    [Fact]
    public async Task Juros_sao_calculados_na_data_de_hoje()
    {
        var calculo = await _cliente.GetFromJsonAsync<CalculoJuros>("/api/juros?valor=1000&dataVencimento=2026-09-26", Json);

        Assert.Equal(new DateOnly(2026, 10, 6), calculo!.DataCalculo);
        Assert.Equal(10, calculo.DiasAtraso);
        Assert.Equal(250m, calculo.ValorJuros);
        Assert.Equal(1250m, calculo.ValorAtualizado);
    }

    [Theory]
    [InlineData("/api/juros")]
    [InlineData("/api/juros?valor=0&dataVencimento=2026-09-26")]
    [InlineData("/api/juros?valor=1000&dataVencimento=26/09/2026")]
    public async Task Juros_com_parametros_invalidos_devolve_400(string url)
    {
        var resposta = await _cliente.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Data_em_formato_errado_tem_mensagem_em_portugues()
    {
        var resposta = await _cliente.GetAsync("/api/juros?valor=1000&dataVencimento=26/09/2026");

        var problema = await resposta.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal("O valor '26/09/2026' não é válido.", Assert.Single(problema!.Errors["DataVencimento"]));
    }
}
