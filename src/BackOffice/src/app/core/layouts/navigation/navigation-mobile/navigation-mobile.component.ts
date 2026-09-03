import {Component, effect, inject, input, Renderer2, signal} from '@angular/core';
import {Router} from "@angular/router";

import {NavigationItemInterface} from "../../../../shared/interfaces/navigationItem.interface";
import {AuthSessionService} from "../../../../shared/auth/auth-session.service";

@Component({
    selector: 'app-navigation-mobile',
    templateUrl: './navigation-mobile.component.html',
    standalone: false
})
export class NavigationMobileComponent {
  NavigationItems = input.required<NavigationItemInterface[]>();

  private readonly router = inject(Router);
  private readonly renderer = inject(Renderer2);
  private readonly authSession = inject(AuthSessionService);

  protected readonly isAuthenticated = this.authSession.isAuthenticated;
  protected readonly displayName = this.authSession.displayName;
  protected readonly email = this.authSession.email;
  protected readonly initials = this.authSession.initials;

  isMobileNavigationOpen = signal(false);
  subNavigation = signal<NavigationItemInterface[] | null>(null);
  baseUrl = signal<string>('')
  titleSubNav = signal<string>('')
  subNavBefore = signal<(NavigationItemInterface[] | null)[]>([])
  titleBefore = signal<string[]>([])

  constructor() {
    effect(() => {
      if (this.isMobileNavigationOpen()) {
        this.renderer.addClass(document.body, 'no-scroll');
      } else {
        this.renderer.removeClass(document.body, 'no-scroll');
        this.subNavigation.set(null);
      }
    }, {allowSignalWrites: true});
  }

  OnMobileNavigationClick() {
    this.isMobileNavigationOpen.set(!this.isMobileNavigationOpen());
    this._resetSubNavigation()
  }

  onBackNavigationClick() {
    this.subNavigation.set(this.subNavBefore().pop() || null);
    this.baseUrl.set(this.baseUrl().replace(/\/[^/]*$/, ''));
    this.titleSubNav.set(this.titleBefore().pop() || '');
  }

  OnSubNavigationClick(item: NavigationItemInterface) {
    if (item.subNav === null || item.subNav.length === 0) {
      this.router.navigateByUrl(this.baseUrl() + item.url);
      this.isMobileNavigationOpen.set(false);
    } else {
      // save before
      this.titleBefore.set([...this.titleBefore(), this.titleSubNav()]);
      this.subNavBefore.set([...this.subNavBefore(), this.subNavigation()]);

      this.baseUrl.set(this.baseUrl() + item.url);
      this.titleSubNav.set(item.title)
      this.subNavigation.set(item.subNav);
    }
  }

  protected logout(): void {
    this.isMobileNavigationOpen.set(false);
    this.authSession.logout().subscribe({error: () => undefined});
  }

  private _resetSubNavigation() {
    this.subNavigation.set(null);
    this.baseUrl.set('');
    this.subNavBefore.set([])
    this.titleBefore.set([])
  }
}
