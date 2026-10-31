import {Component, computed, DestroyRef, inject, OnInit} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {ActivatedRoute} from "@angular/router";
import {buildLotoTableauView} from '@vpd/ui';
import {SseClientService} from "../../shared/clients/sse-client.service";
import {HalloweenPreferences} from './halloween/halloween-preferences';

/** E4 · Tableau du loto en direct, pensé pour le téléphone des joueurs. */
@Component({
    selector: 'app-tableau',
    templateUrl: './tableau.component.html',
    styleUrl: './tableau.component.scss',
    providers: [SseClientService, HalloweenPreferences],
    standalone: false
})
export class TableauComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly live = inject(SseClientService);
  protected readonly preferences = inject(HalloweenPreferences);

  protected readonly view = computed(() => buildLotoTableauView(this.live.eventAsso()));
  protected readonly connection = this.live.connection;
  protected readonly halloween = computed(() =>
    this.live.eventAsso()?.liveTheme === 'Halloween' && this.preferences.phone());

  ngOnInit(): void {
    this.route.paramMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => {
        const id = params.get('id');
        if (id !== null) {
          void this.live.init(id);
        }
      });
  }
}
