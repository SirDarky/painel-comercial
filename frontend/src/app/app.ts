import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  protected readonly modulos = [
    { titulo: 'Comissões', rota: '/comissoes' },
    { titulo: 'Estoque', rota: '/estoque' },
    { titulo: 'Juros', rota: '/juros' },
  ];
}
