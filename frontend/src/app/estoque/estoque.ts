import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { forkJoin, map } from 'rxjs';
import { mensagemDeErro } from '../shared/erro-api';
import { EstoqueApi, Movimentacao, Produto, TipoMovimentacao } from './estoque-api';

const SUGESTOES: Record<TipoMovimentacao, string[]> = {
  Entrada: [
    'Compra de fornecedor',
    'Devolução de cliente',
    'Transferência recebida',
    'Ajuste de inventário (sobra)',
  ],
  Saida: [
    'Venda ao cliente',
    'Devolução ao fornecedor',
    'Perda / avaria',
    'Consumo interno',
    'Ajuste de inventário (falta)',
  ],
};

/** Mesmo limite por movimentação aplicado pela API. */
const QUANTIDADE_MAXIMA = 1_000_000;

const numeroInteiro: ValidatorFn = (controle) =>
  controle.value === null || Number.isInteger(controle.value) ? null : { inteiro: true };

@Component({
  selector: 'app-estoque',
  imports: [ReactiveFormsModule, DatePipe, DecimalPipe],
  templateUrl: './estoque.html',
  styleUrl: './estoque.css',
})
export class Estoque implements OnInit {
  private readonly api = inject(EstoqueApi);

  protected readonly produtos = signal<Produto[]>([]);
  protected readonly movimentacoes = signal<Movimentacao[]>([]);
  protected readonly carregado = signal(false);
  protected readonly erroCarregamento = signal<string | null>(null);

  protected readonly form = new FormGroup({
    codigoProduto: new FormControl<number | null>(null, Validators.required),
    tipo: new FormControl<TipoMovimentacao>('Entrada', { nonNullable: true }),
    quantidade: new FormControl<number | null>(null, [
      Validators.required,
      Validators.min(1),
      Validators.max(QUANTIDADE_MAXIMA),
      numeroInteiro,
    ]),
    descricao: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
  });

  private readonly valores = toSignal(
    this.form.valueChanges.pipe(map(() => this.form.getRawValue())),
    { initialValue: this.form.getRawValue() },
  );

  protected readonly produtoSelecionado = computed(
    () => this.produtos().find((p) => p.codigoProduto === this.valores().codigoProduto) ?? null,
  );

  /** Antecipa no formulário a regra que a API também valida: saída não pode deixar estoque negativo. */
  protected readonly saidaMaiorQueEstoque = computed(() => {
    const { tipo, quantidade } = this.valores();
    const produto = this.produtoSelecionado();
    return tipo === 'Saida' && produto !== null && quantidade !== null && quantidade > produto.estoque;
  });

  protected readonly sugestoes = computed(() => SUGESTOES[this.valores().tipo]);

  protected readonly salvando = signal(false);
  protected readonly erro = signal<string | null>(null);
  protected readonly ultimaMovimentacao = signal<Movimentacao | null>(null);
  private tentativaPendente: { corpo: string; chave: string } | null = null;

  ngOnInit(): void {
    forkJoin({
      produtos: this.api.listarProdutos(),
      movimentacoes: this.api.listarMovimentacoes(),
    }).subscribe({
      next: ({ produtos, movimentacoes }) => {
        this.produtos.set(produtos);
        this.movimentacoes.set(movimentacoes);
        this.form.controls.codigoProduto.setValue(produtos[0]?.codigoProduto ?? null);
        this.carregado.set(true);
      },
      error: (erro) => this.erroCarregamento.set(mensagemDeErro(erro)),
    });
  }

  protected lancar(): void {
    if (this.form.invalid || this.saidaMaiorQueEstoque()) {
      this.form.markAllAsTouched();
      return;
    }

    const { codigoProduto, tipo, quantidade, descricao } = this.form.getRawValue();
    const nova = { codigoProduto: codigoProduto!, tipo, quantidade: quantidade!, descricao: descricao.trim() };

    // Reenviar os mesmos dados (ex.: depois de uma falha de rede) usa a mesma chave, então a API
    // não lança a movimentação duas vezes. Dados diferentes geram uma chave nova.
    const corpo = JSON.stringify(nova);
    if (this.tentativaPendente?.corpo !== corpo) {
      this.tentativaPendente = { corpo, chave: crypto.randomUUID() };
    }

    this.salvando.set(true);
    this.erro.set(null);

    this.api
      .lancar(nova, this.tentativaPendente.chave)
      .subscribe({
        next: (movimentacao) => {
          this.tentativaPendente = null;
          this.ultimaMovimentacao.set(movimentacao);
          this.movimentacoes.update((lista) =>
            lista.some((m) => m.id === movimentacao.id) ? lista : [movimentacao, ...lista],
          );
          this.produtos.update((lista) =>
            lista.map((p) =>
              p.codigoProduto === movimentacao.codigoProduto
                ? { ...p, estoque: movimentacao.estoqueFinal }
                : p,
            ),
          );
          // Mantém produto e tipo selecionados para agilizar lançamentos em sequência.
          this.form.controls.quantidade.reset();
          this.form.controls.descricao.reset();
          this.salvando.set(false);
        },
        error: (erro) => {
          this.erro.set(mensagemDeErro(erro));
          this.salvando.set(false);
        },
      });
  }

  protected rotuloTipo(tipo: TipoMovimentacao): string {
    return tipo === 'Entrada' ? 'Entrada' : 'Saída';
  }
}
