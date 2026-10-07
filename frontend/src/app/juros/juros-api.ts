import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface CalculoJuros {
  valor: number;
  /** Datas no formato aaaa-mm-dd. */
  dataVencimento: string;
  dataCalculo: string;
  diasAtraso: number;
  percentualDiario: number;
  percentualTotal: number;
  valorJuros: number;
  valorAtualizado: number;
}

@Injectable({ providedIn: 'root' })
export class JurosApi {
  private readonly http = inject(HttpClient);

  /** Juros na data de hoje (a data é definida pela API). */
  calcular(valor: number, dataVencimento: string): Observable<CalculoJuros> {
    return this.http.get<CalculoJuros>('/api/juros', { params: { valor, dataVencimento } });
  }
}
