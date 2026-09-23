import {ChangeDetectionStrategy, Component, EventEmitter, Input, Output} from '@angular/core';

@Component({
  selector: 'app-catalog-auth-prompt',
  standalone: false,
  templateUrl: './catalog-auth-prompt.component.html',
  styleUrls: ['./catalog-auth-prompt.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogAuthPromptComponent {
  @Input() open = false;
  @Input() eyebrow = 'Compte requis';
  @Input() title = 'Connectez-vous pour suivre ce livre';
  @Input() description = 'Créez votre espace gratuit pour ajouter ce titre à votre liste de suivi et être prévenu dès qu’il arrive à la bourse aux livres.';
  @Input() benefits: readonly string[] = [
    'Ajoutez autant de livres que vous voulez à votre liste de suivi',
    'Recevez une alerte dès qu’une édition suivie arrive au catalogue',
    'Accès en un instant, sans mot de passe à retenir',
  ];
  @Input() primaryLabel = 'Se connecter';
  @Input() secondaryLabel = 'Créer un compte';
  @Input() secondaryAsClose = false;
  @Input() hint = 'Le catalogue reste consultable sans compte : seul l’ajout à votre liste de suivi nécessite une connexion.';
  @Output() readonly closed = new EventEmitter<void>();
  @Output() readonly loginRequested = new EventEmitter<void>();
  @Output() readonly registerRequested = new EventEmitter<void>();

  requestClose(): void {
    this.closed.emit();
  }

  requestLogin(): void {
    this.loginRequested.emit();
  }

  requestRegister(): void {
    this.registerRequested.emit();
  }

  requestSecondaryAction(): void {
    if (this.secondaryAsClose) {
      this.requestClose();
      return;
    }

    this.requestRegister();
  }
}
