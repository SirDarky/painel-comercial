import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface Venda {
  vendedor: string;
  valor: number;
}

export interface ListaVendas {
  vendas: Venda[];
}

export interface ComissaoVenda {
  valor: number;
  percentualComissao: number;
  comissao: number;
}

export interface ComissaoVendedor {
  vendedor: string;
  quantidadeVendas: number;
  totalVendido: number;
  totalComissao: number;
  vendas: ComissaoVenda[];
}

export interface RelatorioComissoes {
  vendedores: ComissaoVendedor[];
  totalVendido: number;
  totalComissao: number;
}

@Injectable({ providedIn: 'root' })
export class ComissoesApi {
  private readonly http = inject(HttpClient);

  /** Comissões calculadas a partir das vendas do arquivo vendas.json. */
  calcularDoArquivo(): Observable<RelatorioComissoes> {
    return this.http.get<RelatorioComissoes>('/api/comissoes');
  }

  listarVendasDoArquivo(): Observable<ListaVendas> {
    return this.http.get<ListaVendas>('/api/comissoes/vendas');
  }

  calcular(vendas: ListaVendas): Observable<RelatorioComissoes> {
    return this.http.post<RelatorioComissoes>('/api/comissoes/calculo', vendas);
  }
}

/** Formata as vendas como no vendas.json: uma venda por linha, valores com 2 casas. */
export function formatarListaVendas({ vendas }: ListaVendas): string {
  const linhas = vendas.map(
    (venda) => `    { "vendedor": ${JSON.stringify(venda.vendedor)}, "valor": ${venda.valor.toFixed(2)} }`,
  );
  return `{\n  "vendas": [\n${linhas.join(',\n')}\n  ]\n}`;
}
