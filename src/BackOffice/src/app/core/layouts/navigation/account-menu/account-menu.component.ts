import {Component, ElementRef, HostListener, inject, signal} from '@angular/core';

import {AuthSessionService} from '../../../../shared/auth/auth-session.service';

/**
 * Pastille de compte de la barre de navigation.
 *
 * Le BackOffice n'offrait aucun moyen de savoir sous quel compte on travaillait,
 * ni de se déconnecter : la seule sortie était de vider le navigateur. C'est
 * gênant sur un poste partagé le jour d'un loto, où plusieurs bénévoles se
 * relaient sur la même tablette.
 */
@Component({
  selector: 'app-account-menu',
  templateUrl: './account-menu.component.html',
  standalone: false,
})
export class AccountMenuComponent {
  private readonly authSession = inject(AuthSessionService);
  private readonly host = inject(ElementRef<HTMLElement>);

  protected readonly displayName = this.authSession.displayName;
  protected readonly email = this.authSession.email;
  protected readonly initials = this.authSession.initials;
  protected readonly isAuthenticated = this.authSession.isAuthenticated;

  protected readonly isOpen = signal(false);

  @HostListener('document:click', ['$event'])
  protected onDocumentClick(event: MouseEvent): void {
    if (this.isOpen() && !this.host.nativeElement.contains(event.target as Node)) {
      this.isOpen.set(false);
    }
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    this.isOpen.set(false);
  }

  protected toggle(): void {
    this.isOpen.update(open => !open);
  }

  protected logout(): void {
    this.isOpen.set(false);
    this.authSession.logout().subscribe({error: () => undefined});
  }
}
