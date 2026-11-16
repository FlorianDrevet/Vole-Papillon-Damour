import {provideZonelessChangeDetection} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {of, throwError} from 'rxjs';

import {CatalogAdminApiService} from '../../../core/catalog-admin-api.service';
import {CatalogAdminIsbnNotFoundPage} from '../../../core/catalog.models';
import {IsbnNotFoundViewComponent} from './isbn-not-found-view.component';

describe('IsbnNotFoundViewComponent', () => {
  let fixture: ComponentFixture<IsbnNotFoundViewComponent>;
  let api: jasmine.SpyObj<CatalogAdminApiService>;

  const page: CatalogAdminIsbnNotFoundPage = {
    items: [
      {isbn13: '9782070368228', failureReason: 'Aucune notice bibliographique trouvée pour cet ISBN.', resolveAttempts: 3, lastAttemptAt: '2026-11-12T02:14:00Z'},
      {isbn13: '9782253004226', failureReason: null, resolveAttempts: 1, lastAttemptAt: null},
    ],
    totalCount: 7,
    page: 1,
    pageSize: 5,
  };

  beforeEach(async () => {
    api = jasmine.createSpyObj<CatalogAdminApiService>('CatalogAdminApiService', [
      'getIsbnNotFoundBooks', 'exportIsbnNotFoundCsv',
    ]);
    api.getIsbnNotFoundBooks.and.callFake((_token, filters = {}) => of({...page, page: filters.page ?? 1}));
    api.exportIsbnNotFoundCsv.and.returnValue(of(new Blob(['csv'], {type: 'text/csv'})));

    await TestBed.configureTestingModule({
      declarations: [IsbnNotFoundViewComponent],
      providers: [
        provideZonelessChangeDetection(),
        {provide: CatalogAdminApiService, useValue: api},
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(IsbnNotFoundViewComponent);
    fixture.componentRef.setInput('accessToken', 'admin-token');
  });

  it('loads a five-row page of ISBN without a bibliographic notice', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.getIsbnNotFoundBooks).toHaveBeenCalledWith('admin-token', {page: 1, pageSize: 5});
    const root = fixture.nativeElement as HTMLElement;
    expect(root.textContent).toContain('ISBN non trouvés.');
    expect(root.textContent).toContain('7 ISBN sans notice trouvée');
    expect(root.textContent).toContain('Aucune notice bibliographique trouvée pour cet ISBN.');
    expect(root.textContent).toContain('3 tentatives');
    expect(root.querySelectorAll('[data-testid="isbn-not-found-book"]')).toHaveSize(2);
  });

  it('emits the selected ISBN and actions so the administration page can open or update it', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const opened = jasmine.createSpy('bookSelected');
    const retried = jasmine.createSpy('retryRequested');
    const deleted = jasmine.createSpy('deleteRequested');
    fixture.componentInstance.bookSelected.subscribe(opened);
    fixture.componentInstance.retryRequested.subscribe(retried);
    fixture.componentInstance.deleteRequested.subscribe(deleted);

    const row = fixture.nativeElement.querySelector('[data-testid="isbn-not-found-book"]') as HTMLElement;
    (row.querySelector('[data-testid="isbn-not-found-open-book"]') as HTMLButtonElement).click();
    (row.querySelector('[data-testid="isbn-not-found-retry"]') as HTMLButtonElement).click();
    (row.querySelector('[data-testid="isbn-not-found-delete"]') as HTMLButtonElement).click();

    expect(opened).toHaveBeenCalledOnceWith('9782070368228');
    expect(retried).toHaveBeenCalledOnceWith({isbn13: '9782070368228'});
    expect(deleted).toHaveBeenCalledOnceWith({isbn13: '9782070368228'});
  });

  it('requests the next page and updates the pagination label', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('[data-testid="isbn-not-found-next-page"]') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.getIsbnNotFoundBooks).toHaveBeenCalledWith('admin-token', {page: 2, pageSize: 5});
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Page 2 / 2');
  });

  it('disables export and shows the empty state when no ISBN remain', async () => {
    api.getIsbnNotFoundBooks.and.returnValue(of({items: [], totalCount: 0, page: 1, pageSize: 5}));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement.querySelector('[data-testid="isbn-not-found-export"]') as HTMLButtonElement).disabled).toBeTrue();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Aucun ISBN sans notice');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'Les ISBN pour lesquels aucune source n’a trouvé de notice apparaîtront ici.',
    );
  });

  it('downloads the CSV with the dated filename', async () => {
    const now = new Date();
    const expectedDate = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
    const createObjectUrl = spyOn(URL, 'createObjectURL').and.returnValue('blob:isbn-not-found');
    const revokeObjectUrl = spyOn(URL, 'revokeObjectURL');
    let downloadedFilename = '';
    spyOn(HTMLAnchorElement.prototype, 'click').and.callFake(function (this: HTMLAnchorElement) {
      downloadedFilename = this.download;
    });

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('[data-testid="isbn-not-found-export"]') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(api.exportIsbnNotFoundCsv).toHaveBeenCalledOnceWith('admin-token');
    expect(createObjectUrl).toHaveBeenCalledOnceWith(jasmine.any(Blob));
    expect(downloadedFilename).toBe(`isbn-non-trouves-${expectedDate}.csv`);
    expect(revokeObjectUrl).toHaveBeenCalledOnceWith('blob:isbn-not-found');
  });

  it('expands the file-format help with the 13 CSV columns', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('[data-testid="isbn-not-found-format-help"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    const headers = [...root.querySelectorAll('[data-testid="isbn-not-found-format-column"]')]
      .map(element => element.textContent?.trim());
    expect(headers).toEqual([
      'ISBN', 'Titre', 'Auteurs', 'Editeur', 'Annee_publication', 'Format', 'Langue',
      'Genre_principal', 'Genres', 'Resume', 'Pages', 'Edition', 'Couverture_URL',
    ]);
    expect(root.textContent).toContain('CSV séparé par des points-virgules, UTF-8');
    expect(root.querySelector('[data-testid="isbn-not-found-format-help"]')?.getAttribute('aria-expanded')).toBe('true');
  });

  it('shows the loading and error states for the list request', async () => {
    api.getIsbnNotFoundBooks.and.returnValue(throwError(() => new Error('offline')));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')?.textContent).toContain(
      'Impossible de charger les ISBN non trouvés. Réessayez dans un instant.',
    );
  });
});
