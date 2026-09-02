import { Component, input } from '@angular/core';

/**
 * Titre de sous-groupe (mois d'actualités, "Les lotos" / "Les bourses aux
 * livres", "Détails" d'un évènement…) : titre serif + filet fin. Remplace le
 * motif répété `<h2 class="text-8xl">…</h2><div class="h-2 bg-primary-color">`.
 */
@Component({
  selector: 'app-group-heading',
  templateUrl: './group-heading.component.html',
  standalone: false,
})
export class GroupHeadingComponent {
  title = input.required<string>();
}
