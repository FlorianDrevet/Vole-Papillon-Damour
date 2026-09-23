import {ChangeDetectionStrategy, Component, computed, input, signal} from '@angular/core';
import {Router} from '@angular/router';

import {CatalogAuthService} from '../../../core/catalog-auth.service';
import {CatalogSelectionService} from '../../../core/selection/catalog-selection.service';
import {SelectionRef, selectionKey} from '../../../core/selection/selection-merge';

@Component({
  selector: 'app-selection-button',
  standalone: false,
  templateUrl: './selection-button.component.html',
  styleUrls: ['./selection-button.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SelectionButtonComponent {
  readonly ref = input.required<SelectionRef>();
  readonly title = input.required<string>();
  readonly helpText = input('Ma sélection ne réserve rien : le livre peut être vendu avant votre arrivée.');

  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly signInPromptOpen = signal(false);
  readonly inSelection = computed(() => this.selection.keys().has(selectionKey(this.ref())));
  readonly localSelection = computed(() => this.selection.mode() === 'local');
  readonly noBenefits: readonly string[] = [];

  constructor(
    private readonly selection: CatalogSelectionService,
    private readonly auth: CatalogAuthService,
    private readonly router: Router,
  ) {}

  add(): Promise<void> {
    return this.perform(() => this.selection.add(this.ref(), this.title()));
  }

  remove(): Promise<void> {
    return this.perform(() => this.selection.remove(this.ref()));
  }

  openSignInPrompt(event: MouseEvent): void {
    event.preventDefault();
    this.signInPromptOpen.set(true);
  }

  closeSignInPrompt(): void {
    this.signInPromptOpen.set(false);
  }

  async startSignIn(): Promise<void> {
    this.signInPromptOpen.set(false);
    this.error.set(null);
    try {
      await this.auth.login(this.router.url);
    } catch {
      this.error.set('La connexion n’a pas pu être démarrée. Réessayez.');
    }
  }

  private async perform(action: () => Promise<void>): Promise<void> {
    if (this.busy()) {
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    try {
      await action();
    } catch {
      this.error.set('Impossible de mettre à jour Ma sélection. Réessayez.');
    } finally {
      this.busy.set(false);
    }
  }
}
