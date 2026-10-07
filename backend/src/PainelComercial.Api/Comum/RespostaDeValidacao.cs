using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace PainelComercial.Api.Comum;

/// <summary>
/// Respostas 400 em português e sem detalhes internos (nomes de classes, posição no JSON etc.).
/// </summary>
public static class RespostaDeValidacao
{
    /// <summary>Mensagens usadas quando um valor não pode ser convertido (ex.: data "26/09/2026").</summary>
    public static void TraduzirMensagens(DefaultModelBindingMessageProvider mensagens)
    {
        mensagens.SetAttemptedValueIsInvalidAccessor((valor, campo) => $"O valor '{valor}' não é válido para {campo}.");
        mensagens.SetNonPropertyAttemptedValueIsInvalidAccessor(valor => $"O valor '{valor}' não é válido.");
        mensagens.SetUnknownValueIsInvalidAccessor(campo => $"Valor inválido para {campo}.");
        mensagens.SetNonPropertyUnknownValueIsInvalidAccessor(() => "Valor inválido.");
        mensagens.SetValueIsInvalidAccessor(valor => $"O valor '{valor}' é inválido.");
        mensagens.SetValueMustBeANumberAccessor(campo => $"{campo} deve ser um número.");
        mensagens.SetNonPropertyValueMustBeANumberAccessor(() => "O valor deve ser um número.");
        mensagens.SetValueMustNotBeNullAccessor(valor => $"O valor '{valor}' não pode ser vazio.");
        mensagens.SetMissingBindRequiredValueAccessor(campo => $"Informe {campo}.");
        mensagens.SetMissingKeyOrValueAccessor(() => "Informe o valor.");
        mensagens.SetMissingRequestBodyRequiredValueAccessor(() => "Envie o corpo da requisição em JSON.");
    }

    /// <summary>Substitui a resposta 400 automática do [ApiController].</summary>
    public static IActionResult Criar(ActionContext contexto)
    {
        var problema = contexto.HttpContext.RequestServices
            .GetRequiredService<ProblemDetailsFactory>()
            .CreateValidationProblemDetails(contexto.HttpContext, contexto.ModelState, title: "Dados inválidos.");

        problema.Errors.Clear();
        foreach (var (chave, entrada) in contexto.ModelState)
        {
            if (entrada.Errors.Count == 0)
                continue;

            var campo = NomeDoCampo(chave);
            problema.Errors[campo] = entrada.Errors
                // Erro sem mensagem = JSON que não pôde ser convertido; a mensagem original citaria tipos internos.
                .Select(erro => string.IsNullOrEmpty(erro.ErrorMessage) ? MensagemDeFormatoInvalido(campo) : erro.ErrorMessage)
                .Distinct()
                .ToArray();
        }

        return new BadRequestObjectResult(problema) { ContentTypes = { "application/problem+json" } };
    }

    // "$.quantidade" → "quantidade"; "$" ou "" (corpo inteiro) → "corpo".
    private static string NomeDoCampo(string chave) => chave switch
    {
        "" or "$" => "corpo",
        _ when chave.StartsWith("$.") => chave[2..],
        _ => chave
    };

    private static string MensagemDeFormatoInvalido(string campo) => campo switch
    {
        "corpo" => "O corpo da requisição não é um JSON válido.",
        _ when campo.Contains('[') => "Valor em formato inválido.",
        _ => $"Valor em formato inválido para o campo '{campo}'."
    };
}
