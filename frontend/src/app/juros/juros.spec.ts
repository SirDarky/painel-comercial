import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Juros } from './juros';
import { CalculoJuros } from './juros-api';

describe('Juros', () => {
  let fixture: ComponentFixture<Juros>;
  let http: HttpTestingController;

  const tela = () => fixture.nativeElement as HTMLElement;
  const preencher = (seletor: string, valor: string) => {
    const campo = tela().querySelector<HTMLInputElement>(seletor)!;
    campo.value = valor;
    campo.dispatchEvent(new Event('input'));
  };
  const calcular = () => tela().querySelector<HTMLButtonElement>('button[type="submit"]')!.click();

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [Juros],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Juros);
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  it('envia valor e vencimento e mostra o resultado calculado pela API', async () => {
    preencher('#valor', '1000');
    preencher('#vencimento', '2026-09-26');
    calcular();

    const requisicao = http.expectOne((r) => r.url === '/api/juros');
    expect(requisicao.request.params.get('valor')).toBe('1000');
    expect(requisicao.request.params.get('dataVencimento')).toBe('2026-09-26');

    requisicao.flush({
      valor: 1000,
      dataVencimento: '2026-09-26',
      dataCalculo: '2026-10-06',
      diasAtraso: 10,
      percentualDiario: 2.5,
      percentualTotal: 25,
      valorJuros: 250,
      valorAtualizado: 1250,
    } satisfies CalculoJuros);
    await fixture.whenStable();

    expect(tela().querySelector('.badge')?.textContent?.trim()).toBe('Vencido há 10 dias');
    expect(tela().querySelector('.linhas__juros dd')?.textContent).toContain('250.00');
  });

  it('não chama a API com valor fora do limite ou sem vencimento', async () => {
    preencher('#valor', '10000000000000');
    calcular();
    await fixture.whenStable();

    http.expectNone((r) => r.url === '/api/juros');
    const erros = Array.from(tela().querySelectorAll('.erro-campo'), (e) => e.textContent?.trim());
    expect(erros).toEqual([
      'O valor máximo é R$ 1.000.000.000.000,00.',
      'Informe a data de vencimento.',
    ]);
  });
});
