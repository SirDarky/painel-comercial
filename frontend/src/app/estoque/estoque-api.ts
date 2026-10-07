import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export type TipoMovimentacao = 'Entrada' | 'Saida';

export interface Produto {
  codigoProduto: number;
  descricaoProduto: string;
  estoque: number;
}

export interface NovaMovimentacao {
  codigoProduto: number;
  tipo: TipoMovimentacao;
  quantidade: number;
  descricao: string;
}

export interface Movimentacao extends NovaMovimentacao {
  id: number;
  descricaoProduto: string;
  dataHora: string;
  estoqueAnterior: number;
  estoqueFinal: number;
}

@Injectable({ providedIn: 'root' })
export class EstoqueApi {
  private readonly http = inject(HttpClient);

  listarProdutos(): Observable<Produto[]> {
    return this.http.get<Produto[]>('/api/estoque/produtos');
  }

  listarMovimentacoes(): Observable<Movimentacao[]> {
    return this.http.get<Movimentacao[]>('/api/estoque/movimentacoes');
  }

  /**
   * Lança a movimentação; a resposta traz o número gerado e o estoque final do produto.
   * Repetir a chamada com a mesma `chaveIdempotencia` devolve a movimentação já lançada.
   */
  lancar(movimentacao: NovaMovimentacao, chaveIdempotencia: string): Observable<Movimentacao> {
    return this.http.post<Movimentacao>('/api/estoque/movimentacoes', movimentacao, {
      headers: { 'Idempotency-Key': chaveIdempotencia },
    });
  }
}
