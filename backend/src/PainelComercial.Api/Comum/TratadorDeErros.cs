using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace PainelComercial.Api.Comum;

/// <summary>
/// Converte exceções conhecidas em respostas ProblemDetails (RFC 9457), para que o frontend
/// receba sempre uma mensagem legível no campo <c>detail</c>. Demais exceções viram 500 genérico.
/// </summary>
public sealed class TratadorDeErros(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        (int Status, string Titulo, string Detalhe)? erro = exception switch
        {
            RecursoNaoEncontradoException => (StatusCodes.Status404NotFound, "Recurso não encontrado", exception.Message),
            ConflitoException => (StatusCodes.Status409Conflict, "Conflito", exception.Message),
            RegraNegocioException => (StatusCodes.Status422UnprocessableEntity, "Regra de negócio violada", exception.Message),
            BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } =>
                (StatusCodes.Status413PayloadTooLarge, "Requisição muito grande",
                    "O corpo da requisição ultrapassa o tamanho máximo permitido (1 MB)."),
            BadHttpRequestException requisicaoInvalida =>
                (requisicaoInvalida.StatusCode, "Requisição inválida", "Não foi possível ler a requisição."),
            _ => null
        };

        if (erro is null)
            return false;

        var (status, titulo, detalhe) = erro.Value;
        httpContext.Response.StatusCode = status;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = titulo,
                Detail = detalhe
            }
        });
    }
}
