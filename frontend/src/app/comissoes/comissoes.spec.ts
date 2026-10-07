import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Comissoes } from './comissoes';
import { RelatorioComissoes } from './comissoes-api';

describe('Comissoes', () => {
  let fixture: ComponentFixture<Comissoes>;
  let http: HttpTestingController;

  const relatorio: RelatorioComissoes = {
    vendedores: [
      {
        vendedor: 'Ana Lima',
        quantidadeVendas: 2,
        totalVendido: 700,
        totalComissao: 27,
        vendas: [
          { valor: 200, percentualComissao: 1, comissao: 2 },
          { valor: 500, percentualComissao: 5, comissao: 25 },
        ],
      },
    ],
    totalVendido: 700,
    totalComissao: 27,
  };

  const tela = () => fixture.nativeElement as HTMLElement;
  const botao = (texto: string) =>
    Array.from(tela().querySelectorAll('button')).find((b) => b.textContent?.includes(texto))!;
  const editarJson = (json: string) => {
    const editor = tela().querySelector<HTMLTextAreaElement>('#json-vendas')!;
    editor.value = json;
    editor.dispatchEvent(new Event('input'));
  };

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [Comissoes],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);

    fixture = TestBed.createComponent(Comissoes);
    await fixture.whenStable();
    http.expectOne('/api/comissoes').flush(relatorio);
    http.expectOne('/api/comissoes/vendas').flush({
      vendas: [
        { vendedor: 'Ana Lima', valor: 200 },
        { vendedor: 'Ana Lima', valor: 500 },
      ],
    });
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  it('mostra a comissão do vendedor e as vendas ao expandir', async () => {
    const linha = tela().querySelector('tbody tr')!.textContent!;
    expect(linha).toContain('Ana Lima');
    expect(linha).toContain('27.00');

    botao('Ana Lima').click();
    await fixture.whenStable();

    const faixas = Array.from(tela().querySelectorAll('.detalhe .badge'), (b) => b.textContent);
    expect(faixas).toEqual(['1%', '5%']);
  });

  it('preenche o editor com as vendas do arquivo', () => {
    const editor = tela().querySelector<HTMLTextAreaElement>('#json-vendas')!;
    expect(JSON.parse(editor.value)).toEqual({
      vendas: [
        { vendedor: 'Ana Lima', valor: 200 },
        { vendedor: 'Ana Lima', valor: 500 },
      ],
    });
    expect(editor.value).toContain('"valor": 200.00');
  });

  it('não chama a API quando o JSON é inválido ou está fora do formato', async () => {
    editarJson('{ vendas: ');
    botao('Calcular comissões').click();
    await fixture.whenStable();
    expect(tela().querySelector('.editor .alerta')?.textContent).toContain('JSON inválido');

    editarJson('[]');
    botao('Calcular comissões').click();
    await fixture.whenStable();
    expect(tela().querySelector('.editor .alerta')?.textContent).toContain('O JSON deve ter o formato');

    http.expectNone('/api/comissoes/calculo');
  });

  it('calcula com as vendas editadas', async () => {
    editarJson('{ "vendas": [ { "vendedor": "Bruno", "valor": 99.99 } ] }');
    botao('Calcular comissões').click();

    const requisicao = http.expectOne('/api/comissoes/calculo');
    expect(requisicao.request.body).toEqual({ vendas: [{ vendedor: 'Bruno', valor: 99.99 }] });
    requisicao.flush({
      vendedores: [
        {
          vendedor: 'Bruno',
          quantidadeVendas: 1,
          totalVendido: 99.99,
          totalComissao: 0,
          vendas: [{ valor: 99.99, percentualComissao: 0, comissao: 0 }],
        },
      ],
      totalVendido: 99.99,
      totalComissao: 0,
    } satisfies RelatorioComissoes);
    await fixture.whenStable();

    expect(tela().querySelector('tbody tr')!.textContent).toContain('Bruno');
    expect(tela().textContent).toContain('Vendas informadas no editor');
  });
});
