import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { mensagemDeErro } from '../shared/erro-api';
import { CalculoJuros, JurosApi } from './juros-api';

/** Mesmo limite aplicado pela API. */
const VALOR_MAXIMO = 1_000_000_000_000;

@Component({
  selector: 'app-juros',
  imports: [ReactiveFormsModule, CurrencyPipe, DatePipe, DecimalPipe],
  templateUrl: './juros.html',
  styleUrl: './juros.css',
})
export class Juros {
  private readonly api = inject(JurosApi);

  protected readonly form = new FormGroup({
    valor: new FormControl<number | null>(null, [
      Validators.required,
      Validators.min(0.01),
      Validators.max(VALOR_MAXIMO),
    ]),
    dataVencimento: new FormControl('', { nonNullable: true, validators: Validators.required }),
  });

  protected readonly resultado = signal<CalculoJuros | null>(null);
  protected readonly calculando = signal(false);
  protected readonly erro = signal<string | null>(null);

  protected calcular(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { valor, dataVencimento } = this.form.getRawValue();
    this.calculando.set(true);
    this.erro.set(null);

    this.api.calcular(valor!, dataVencimento).subscribe({
      next: (resultado) => {
        this.resultado.set(resultado);
        this.calculando.set(false);
      },
      error: (erro) => {
        this.resultado.set(null);
        this.erro.set(mensagemDeErro(erro));
        this.calculando.set(false);
      },
    });
  }
}
