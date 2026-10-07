import { HttpErrorResponse } from '@angular/common/http';

/** Formato de erro devolvido pela API (ProblemDetails / ValidationProblemDetails do ASP.NET Core). */
interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

/** Converte um erro HTTP da API em uma mensagem para exibir ao usuário. */
export function mensagemDeErro(erro: unknown): string {
  if (!(erro instanceof HttpErrorResponse)) {
    return 'Ocorreu um erro inesperado.';
  }

  const problema: ProblemDetails | null =
    typeof erro.error === 'object' && erro.error !== null ? erro.error : null;

  if (problema?.errors) {
    // Campos de listas (ex.: "Vendas[3].Valor") ganham o nome como prefixo para localizar o item.
    return Object.entries(problema.errors)
      .flatMap(([campo, mensagens]) =>
        mensagens.map((mensagem) => (campo.includes('[') ? `${campo}: ${mensagem}` : mensagem)),
      )
      .join('\n');
  }

  if (problema?.detail) {
    return problema.detail;
  }

  if (erro.status === 0 || erro.status >= 500) {
    return 'Não foi possível falar com a API. Verifique se o backend está rodando (dotnet run).';
  }

  return problema?.title ?? `A API respondeu com o erro ${erro.status}.`;
}
