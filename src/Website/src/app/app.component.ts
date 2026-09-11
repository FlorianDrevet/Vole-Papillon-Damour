import {Component, OnDestroy, OnInit, signal} from '@angular/core';
import {NavigationCancel, NavigationEnd, NavigationError, NavigationStart, Router} from '@angular/router';
import {Subject, filter, takeUntil} from 'rxjs';

@Component({
    selector: 'app-root',
    templateUrl: './app.component.html',
    styleUrl: './app.component.scss',
    standalone: false
})
export class AppComponent implements OnInit, OnDestroy {
  title = "Vole Papillon d'Amour";
  readonly isNavigating = signal(false);

  private readonly destroyed = new Subject<void>();

  constructor(private readonly router: Router) {}

  ngOnInit(): void {
    this.router.events
      .pipe(
        filter(event =>
          event instanceof NavigationStart ||
          event instanceof NavigationEnd ||
          event instanceof NavigationCancel ||
          event instanceof NavigationError,
        ),
        takeUntil(this.destroyed),
      )
      .subscribe(event => this.isNavigating.set(event instanceof NavigationStart));
  }

  ngOnDestroy(): void {
    this.destroyed.next();
    this.destroyed.complete();
  }
}
