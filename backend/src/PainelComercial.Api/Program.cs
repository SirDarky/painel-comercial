using System.Text.Json.Serialization;
using PainelComercial.Api.Comissoes;
using PainelComercial.Api.Comum;
using PainelComercial.Api.Estoque;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Nenhuma requisição desta API precisa de mais que 1 MB (10 mil vendas cabem em ~500 KB).
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = 1024 * 1024);

builder.Services
    .AddControllers(opcoes =>
    {
        // Os campos obrigatórios já têm [Required] com mensagem própria.
        opcoes.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        RespostaDeValidacao.TraduzirMensagens(opcoes.ModelBindingMessageProvider);
    })
    .AddJsonOptions(opcoes =>
    {
        opcoes.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        opcoes.AllowInputFormatterExceptionMessages = false; // não expõe detalhes internos do JSON inválido
    })
    .ConfigureApiBehaviorOptions(opcoes =>
    {
        opcoes.InvalidModelStateResponseFactory = RespostaDeValidacao.Criar;
        opcoes.ClientErrorMapping[StatusCodes.Status404NotFound].Title = "Recurso não encontrado";
        opcoes.ClientErrorMapping[StatusCodes.Status415UnsupportedMediaType].Title = "Envie os dados em JSON (Content-Type: application/json)";
    });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<TratadorDeErros>();
builder.Services.AddLimiteDeRequisicoes(builder.Configuration);
builder.Services.AddOpenApi();

// Os dados iniciais vêm dos arquivos JSON da pasta Data.
var pastaDados = Path.Combine(builder.Environment.ContentRootPath, "Data");

var fusoHorario = TimeZoneInfo.FindSystemTimeZoneById(builder.Configuration["FusoHorario"] ?? "America/Sao_Paulo");
builder.Services.AddSingleton<TimeProvider>(new RelogioNoFuso(fusoHorario));
builder.Services.AddSingleton(new ListaVendas(ArquivosDados.LerVendas(pastaDados)));

// Produtos e movimentações ficam no SQLite. Um caminho relativo na connection string
// é resolvido a partir da pasta da API.
builder.Services.AddDbContext<EstoqueDbContext>((servicos, opcoes) =>
{
    var conexao = new SqliteConnectionStringBuilder(
        servicos.GetRequiredService<IConfiguration>().GetConnectionString("Estoque")
        ?? throw new InvalidOperationException("Connection string 'Estoque' não configurada."));
    conexao.DataSource = Path.Combine(builder.Environment.ContentRootPath, conexao.DataSource);
    opcoes.UseSqlite(conexao.ConnectionString);
});
builder.Services.AddScoped<EstoqueService>();

var app = builder.Build();

// Cria ou atualiza o banco (migrations) e, se ainda não houver produtos, carrega o estoque.json.
await using (var escopo = app.Services.CreateAsyncScope())
{
    var estoqueDb = escopo.ServiceProvider.GetRequiredService<EstoqueDbContext>();
    await estoqueDb.Database.MigrateAsync();

    if (!await estoqueDb.Produtos.AnyAsync())
    {
        estoqueDb.Produtos.AddRange(ArquivosDados.LerEstoque(pastaDados));
        await estoqueDb.SaveChangesAsync();
    }
}

app.UseExceptionHandler();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // documentação interativa em /scalar
}

app.MapControllers();

app.Run();
