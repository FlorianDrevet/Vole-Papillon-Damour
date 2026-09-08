import {Injectable, signal} from '@angular/core';
import {Observable, catchError, map, of, shareReplay, tap} from 'rxjs';

import {CatalogApiService} from './catalog-api.service';
import {mergeCatalogGenres} from './catalog-genres';

@Injectable({providedIn: 'root'})
export class CatalogGenresService {
  readonly genres = signal<readonly string[]>([]);
  private genresRequest?: Observable<readonly string[]>;

  constructor(private readonly api: CatalogApiService) {}

  load(): void {
    if (this.genresRequest) {
      return;
    }

    this.genresRequest = this.api.search({pageSize: 1}).pipe(
      map(response => mergeCatalogGenres(response.genres)),
      tap(genres => this.genres.set(genres)),
      catchError(() => of([] as readonly string[])),
      shareReplay({bufferSize: 1, refCount: false}),
    );
    this.genresRequest.subscribe();
  }
}
