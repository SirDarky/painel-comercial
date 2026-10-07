import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Estoque } from './estoque';
import { Movimentacao } from './estoque-api';

describe('Estoque', () => {
  let fixture: ComponentFixture<Estoque>;
  let http: HttpTestingController;

  const tela = () => fixture.nativeElement as HTMLElement;

  const preencher = (seletor: string, valor: string) => {
    const campo = tela().querySelector<HTMLInputElement>(seletor)!;
    campo.value = valor;
    campo.dispatchEvent(new Event('input'));
  };

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [Estoque],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);

    fixture = TestBed.createComponent(Estoque);
    await fixture.whenStable();
    http
      .expectOne('/api/estoque/produtos')
      .flush([{ codigoProduto: 101, descricaoProduto: 'Caneta Azul', estoque: 150 }]);
    http.expectOne('/api/estoque/movimentacoes').flush([]);
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  it('lança a movimentação e mostra o estoque final devolvido pela API', async () => {
    tela().querySelector<HTMLInputElement>('input[value="Saida"]')!.click();
    preencher('#quantidade', '10');
    preencher('#descricao', 'Venda ao cliente');
    tela().querySelector<HTMLButtonElement>('button[type="submit"]')!.click();

    const requisicao = http.expectOne('/api/estoque/movimentacoes');
    expect(requisicao.request.method).toBe('POST');
    expect(requisicao.request.body).toEqual({
      codigoProduto: 101,
      tipo: 'Saida',
      quantidade: 10,
      descricao: 'Venda ao cliente',
    });

    requisicao.flush({
      id: 1,
      codigoProduto: 101,
      descricaoProduto: 'Caneta Azul',
      tipo: 'Saida',
      quantidade: 10,
      descricao: 'Venda ao cliente',
      dataHora: '2026-10-06T09:30:00-03:00',
      estoqueAnterior: 150,
      estoqueFinal: 140,
    } satisfies Movimentacao);
    await fixture.whenStable();

    expect(tela().querySelector('.resultado__titulo')?.textContent).toBe(
      'Movimentação nº 1 registrada',
    );
    expect(tela().querySelector('.resultado__estoque strong')?.textContent).toBe('140');
  });

  it('reenvia com a mesma Idempotency-Key depois de uma falha de rede', async () => {
    preencher('#quantidade', '10');
    preencher('#descricao', 'Compra de fornecedor');

    tela().querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    const primeira = http.expectOne('/api/estoque/movimentacoes');
    primeira.error(new ProgressEvent('error'));
    await fixture.whenStable();

    tela().querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    const segunda = http.expectOne('/api/estoque/movimentacoes');

    const chave = primeira.request.headers.get('Idempotency-Key');
    expect(chave).toBeTruthy();
    expect(segunda.request.headers.get('Idempotency-Key')).toBe(chave);
    segunda.flush({ message: 'erro' }, { status: 500, statusText: 'Erro' });
    await fixture.whenStable();

    // Mudar os dados gera uma movimentação nova, com outra chave.
    preencher('#quantidade', '11');
    tela().querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    const terceira = http.expectOne('/api/estoque/movimentacoes');
    expect(terceira.request.headers.get('Idempotency-Key')).not.toBe(chave);
    terceira.flush({ message: 'erro' }, { status: 500, statusText: 'Erro' });
  });

  it('não envia quantidade com casas decimais', async () => {
    preencher('#quantidade', '1.5');
    preencher('#descricao', 'Compra de fornecedor');
    tela().querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await fixture.whenStable();

    http.expectNone('/api/estoque/movimentacoes');
    expect(tela().querySelector('.erro-campo')?.textContent).toContain('quantidade inteira');
  });

  it('não envia uma saída maior que o estoque atual', async () => {
    tela().querySelector<HTMLInputElement>('input[value="Saida"]')!.click();
    preencher('#quantidade', '151');
    preencher('#descricao', 'Venda ao cliente');
    tela().querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await fixture.whenStable();

    http.expectNone('/api/estoque/movimentacoes');
    expect(tela().textContent).toContain('A saída não pode ser maior que o estoque atual');
  });
});
