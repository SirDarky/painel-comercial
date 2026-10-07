import { HttpErrorResponse } from '@angular/common/http';
import { mensagemDeErro } from './erro-api';

describe('mensagemDeErro', () => {
  it('usa o detail do ProblemDetails', () => {
    const erro = new HttpErrorResponse({
      status: 422,
      error: { title: 'Regra de negócio violada', detail: 'Estoque insuficiente.' },
    });

    expect(mensagemDeErro(erro)).toBe('Estoque insuficiente.');
  });

  it('lista as mensagens de validação, identificando itens de listas', () => {
    const erro = new HttpErrorResponse({
      status: 400,
      error: {
        title: 'One or more validation errors occurred.',
        errors: {
          Quantidade: ['A quantidade deve ser maior que zero.'],
          'Vendas[2].Valor': ['O valor da venda deve ser positivo.'],
        },
      },
    });

    expect(mensagemDeErro(erro)).toBe(
      'A quantidade deve ser maior que zero.\nVendas[2].Valor: O valor da venda deve ser positivo.',
    );
  });

  it('avisa quando a API não responde', () => {
    expect(mensagemDeErro(new HttpErrorResponse({ status: 0 }))).toContain(
      'Não foi possível falar com a API',
    );
  });
});
