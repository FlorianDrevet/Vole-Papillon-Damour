import {ChangeDetectionStrategy, Component, OnInit} from '@angular/core';
import {Meta, Title} from '@angular/platform-browser';

@Component({
  selector: 'app-catalog-not-found-page',
  standalone: false,
  templateUrl: './catalog-not-found-page.component.html',
  styleUrls: ['./catalog-not-found-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogNotFoundPageComponent implements OnInit {
  constructor(
    private readonly title: Title,
    private readonly meta: Meta,
  ) {}

  ngOnInit(): void {
    this.title.setTitle('Page introuvable · Bourse aux livres');
    this.meta.updateTag({name: 'robots', content: 'noindex, nofollow'});
  }
}
