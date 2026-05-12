import {Component, inject, OnInit, signal} from '@angular/core';
import {Router} from "@angular/router";

@Component({
  selector: 'app-footer',
  templateUrl: './footer.component.html',
  styleUrl: './footer.component.scss'
})
export class FooterComponent implements OnInit {
  url = signal<string>('');
  route = inject(Router);

  ngOnInit(): void {
    this.route.events.subscribe(() => {
      this.url.set(this.route.url);
    });
  }
}
