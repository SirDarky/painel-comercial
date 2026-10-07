namespace PainelComercial.Api.Comum;

/// <summary>
/// Violação de uma regra de negócio (ex.: saída maior que o estoque disponível).
/// É devolvida ao cliente como HTTP 422 pelo <see cref="TratadorDeErros"/>.
/// </summary>
public class RegraNegocioException(string mensagem) : Exception(mensagem);

/// <summary>O recurso solicitado não existe. É devolvida ao cliente como HTTP 404.</summary>
public sealed class RecursoNaoEncontradoException(string mensagem) : RegraNegocioException(mensagem);

/// <summary>A requisição conflita com uma operação já registrada. É devolvida ao cliente como HTTP 409.</summary>
public sealed class ConflitoException(string mensagem) : RegraNegocioException(mensagem);
