import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { mensagemDeErro } from '../shared/erro-api';
import {
  ComissoesApi,
  ListaVendas,
  RelatorioComissoes,
  formatarListaVendas,
} from './comissoes-api';

@Component({
  selector: 'app-comissoes',
  imports: [CurrencyPipe, ReactiveFormsModule],
  templateUrl: './comissoes.html',
  styleUrl: './comissoes.css',
})
export class Comissoes implements OnInit {
  private readonly api = inject(ComissoesApi);

  protected readonly relatorio = signal<RelatorioComissoes | null>(null);
  protected readonly erro = signal<string | null>(null);
  protected readonly dadosPersonalizados = signal(false);
  protected readonly vendedoresAbertos = signal<ReadonlySet<string>>(new Set());

  protected readonly quantidadeVendas = computed(
    () => this.relatorio()?.vendedores.reduce((total, v) => total + v.quantidadeVendas, 0) ?? 0,
  );

  /** Editor para testar o cálculo com outras vendas. */
  protected readonly json = new FormControl('', { nonNullable: true });
  protected readonly erroJson = signal<string | null>(null);
  protected readonly calculando = signal(false);
  private jsonOriginal = '';

  ngOnInit(): void {
    forkJoin({
      relatorio: this.api.calcularDoArquivo(),
      vendas: this.api.listarVendasDoArquivo(),
    }).subscribe({
      next: ({ relatorio, vendas }) => {
        this.jsonOriginal = formatarListaVendas(vendas);
        this.json.setValue(this.jsonOriginal);
        this.relatorio.set(relatorio);
      },
      error: (erro) => this.erro.set(mensagemDeErro(erro)),
    });
  }

  protected alternarVendedor(vendedor: string): void {
    this.vendedoresAbertos.update((abertos) => {
      const novos = new Set(abertos);
      if (!novos.delete(vendedor)) {
        novos.add(vendedor);
      }
      return novos;
    });
  }

  protected calcularComJson(): void {
    let vendas: ListaVendas;
    try {
      vendas = JSON.parse(this.json.value);
    } catch (erro) {
      this.erroJson.set(`JSON inválido: ${(erro as Error).message}`);
      return;
    }

    // Os valores de cada venda são validados pela API; aqui só confere o formato geral.
    if (!Array.isArray(vendas?.vendas)) {
      this.erroJson.set('O JSON deve ter o formato { "vendas": [ { "vendedor": "...", "valor": 0 } ] }.');
      return;
    }

    this.erroJson.set(null);
    this.calculando.set(true);
    this.api.calcular(vendas).subscribe({
      next: (relatorio) => {
        this.mostrar(relatorio, true);
        this.calculando.set(false);
      },
      error: (erro) => {
        this.erroJson.set(mensagemDeErro(erro));
        this.calculando.set(false);
      },
    });
  }

  protected restaurarVendasOriginais(): void {
    this.json.setValue(this.jsonOriginal);
    this.erroJson.set(null);
    this.api.calcularDoArquivo().subscribe({
      next: (relatorio) => this.mostrar(relatorio, false),
      error: (erro) => this.erroJson.set(mensagemDeErro(erro)),
    });
  }

  private mostrar(relatorio: RelatorioComissoes, personalizado: boolean): void {
    this.relatorio.set(relatorio);
    this.dadosPersonalizados.set(personalizado);
    this.vendedoresAbertos.set(new Set());
  }
}
