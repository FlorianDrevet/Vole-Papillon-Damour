import {Component, effect, input, Renderer2, signal} from '@angular/core';
import {NavigationItemInterface} from "../../../../shared/interfaces/navigationItem.interface";
import {Router} from "@angular/router";

@Component({
    selector: 'app-navigation-mobile',
    templateUrl: './navigation-mobile.component.html',
    styleUrl: './navigation-mobile.component.scss',
    standalone: false
})
export class NavigationMobileComponent {
  Router!: Router
  NavigationItems = input.required<NavigationItemInterface[]>();

  isMobileNavigationOpen = signal(false);
  subNavigation = signal<NavigationItemInterface[] | null>(null);
  baseUrl = signal<string>('')
  titleSubNav = signal<string>('')
  subNavBefore = signal<(NavigationItemInterface[] | null)[]>([])
  titleBefore = signal<string[]>([])

  constructor(private renderer: Renderer2, private router: Router) {
    effect(() => {
      if (this.isMobileNavigationOpen()) {
        this.renderer.addClass(document.body, 'no-scroll');
      } else {
        this.renderer.removeClass(document.body, 'no-scroll');
        this.subNavigation.set(null);
      }
    }, {allowSignalWrites: true});

    this.Router = router;
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

  private _resetSubNavigation() {
    this.subNavigation.set(null);
    this.baseUrl.set('');
    this.subNavBefore.set([])
    this.titleBefore.set([])
  }
}
