import {ChangeDetectionStrategy, Component, OnDestroy, OnInit} from '@angular/core';
import {Meta} from '@angular/platform-browser';
import {NavigationEnd, Router} from '@angular/router';
import {Subject, filter, takeUntil} from 'rxjs';

import {catalogRobotsForUrl} from './core/catalog-robots';

@Component({
  selector: 'app-root',
  standalone: false,
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent implements OnInit, OnDestroy {
  private readonly destroyed = new Subject<void>();

  constructor(
    private readonly router: Router,
    private readonly meta: Meta,
  ) {}

  ngOnInit(): void {
    this.updateRobotsMetadata(this.router.url);
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntil(this.destroyed),
      )
      .subscribe(event => this.updateRobotsMetadata(event.urlAfterRedirects));
  }

  ngOnDestroy(): void {
    this.destroyed.next();
    this.destroyed.complete();
  }

  private updateRobotsMetadata(url: string): void {
    this.meta.updateTag({name: 'robots', content: catalogRobotsForUrl(url)});
  }
}
