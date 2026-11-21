import {NO_ERRORS_SCHEMA} from '@angular/core';
import {CommonModule} from '@angular/common';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {FormsModule} from '@angular/forms';
import {RouterTestingModule} from '@angular/router/testing';

import {ScanRareBook} from '../offline/scan-offline.model';
import {ScanRareBookService} from './scan-rare-book.service';
import {ScanRareBookListComponent} from './scan-rare-book-list.component';

describe('ScanRareBookListComponent', () => {
  let fixture: ComponentFixture<ScanRareBookListComponent>;
  let rareBooks: jasmine.SpyObj<ScanRareBookService>;

  beforeEach(async () => {
    rareBooks = jasmine.createSpyObj<ScanRareBookService>('ScanRareBookService', [
      'list',
      'refreshFromServer',
      'pendingPhotoCount',
    ]);
    rareBooks.list.and.returnValue(Promise.resolve([]));
    rareBooks.refreshFromServer.and.returnValue(Promise.resolve({
      books: [], totalCount: 0, page: 1, pageSize: 24,
    } as never));
    rareBooks.pendingPhotoCount.and.returnValue(Promise.resolve(0));

    await TestBed.configureTestingModule({
      declarations: [ScanRareBookListComponent],
      imports: [CommonModule, FormsModule, RouterTestingModule],
      providers: [{provide: ScanRareBookService, useValue: rareBooks}],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture = TestBed.createComponent(ScanRareBookListComponent);
  });

  it('shows the shared loader while the rare-book list is loading', () => {
    rareBooks.list.and.returnValue(new Promise(() => undefined));

    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('vpd-loader')).not.toBeNull();
  });

  it('shows a retry action when the server list cannot be loaded', async () => {
    spyOnProperty(navigator, 'onLine', 'get').and.returnValue(true);
    rareBooks.refreshFromServer.and.returnValues(
      Promise.reject(new Error('401 Unauthorized')),
      Promise.resolve({books: [], totalCount: 0, page: 1, pageSize: 24} as never),
    );

    fixture.detectChanges();
    await fixture.whenStable();
    await new Promise<void>(resolve => setTimeout(resolve, 0));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.rare-error')?.textContent)
      .toContain('La liste locale reste disponible');
    const retry = fixture.nativeElement.querySelector('.rare-retry') as HTMLButtonElement;
    expect(retry).not.toBeNull();

    retry.click();
    await fixture.whenStable();
    await new Promise<void>(resolve => setTimeout(resolve, 0));
    fixture.detectChanges();

    expect(rareBooks.refreshFromServer).toHaveBeenCalledTimes(2);
    expect(fixture.nativeElement.querySelector('.rare-error')).toBeNull();
  });

  it('keeps local rare books and creation available when server refresh fails', async () => {
    spyOnProperty(navigator, 'onLine', 'get').and.returnValue(true);
    rareBooks.list.and.resolveTo([{
      clientId: 'rare-local-1',
      title: 'Les Fables',
      status: 'Draft',
      isSold: false,
      price: 24,
      syncStatus: 'PendingCreate',
    } as ScanRareBook]);
    rareBooks.refreshFromServer.and.rejectWith(new Error('API unavailable'));

    fixture.detectChanges();
    await fixture.whenStable();
    await new Promise<void>(resolve => setTimeout(resolve, 0));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.rare-card strong')?.textContent).toContain('Les Fables');
    expect(fixture.nativeElement.querySelector('.rare-list-footer .rare-add')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.rare-error')?.textContent)
      .toContain('La liste locale reste disponible');
  });

  it('searches the cached rare-book list locally while offline', async () => {
    spyOnProperty(navigator, 'onLine', 'get').and.returnValue(false);
    fixture.componentInstance.search = 'Actes Sud';
    rareBooks.list.and.resolveTo([{
      clientId: 'rare-local-1',
      title: 'La bibliothèque',
      publisher: 'Actes Sud',
      status: 'Draft',
      isSold: false,
      price: 24,
      syncStatus: 'PendingCreate',
    } as ScanRareBook]);

    fixture.detectChanges();
    await fixture.whenStable();
    await new Promise<void>(resolve => setTimeout(resolve, 0));
    fixture.detectChanges();

    expect(fixture.componentInstance.offline).toBeTrue();
    expect(rareBooks.refreshFromServer).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('.rare-card strong')?.textContent)
      .toContain('La bibliothèque');
  });

  it('shows a dedicated empty state when there are no rare books', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    await new Promise<void>(resolve => setTimeout(resolve, 0));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.rare-empty')?.textContent)
      .toContain('Aucune fiche rare pour le moment.');
  });

  it('keeps the add action in the list flow', () => {
    fixture.componentInstance.loading = false;
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.rare-list-footer .rare-add'))
      .not.toBeNull();
  });

  it('uses the rare-book mockup shell with compact filters', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    await new Promise<void>(resolve => setTimeout(resolve, 0));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.rare-flow-header')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('LIVRES RARES');
    expect(fixture.nativeElement.querySelectorAll('.rare-filter-tab').length).toBe(2);
  });

  it('keeps a long rare-book list inside the viewport and scrollable', () => {
    spyOn(fixture.componentInstance, 'reload').and.resolveTo();
    fixture.componentInstance.loading = false;
    fixture.componentInstance.books = Array.from({length: 12}, (_, index) => ({
      clientId: `rare-${index}`,
      title: `Livre rare ${index}`,
      status: 'Draft',
      isSold: false,
      price: 20,
      syncStatus: 'Synced',
    } as ScanRareBook));
    fixture.detectChanges();

    const viewport = fixture.nativeElement.querySelector('.rare-list-scroll') as HTMLElement;

    expect(viewport.clientHeight).toBeGreaterThan(0);
    expect(viewport.scrollHeight).toBeGreaterThan(viewport.clientHeight);
  });

  it('requests search, draft filter and the selected page from the server', async () => {
    fixture.componentInstance.search = 'Victor Hugo';
    fixture.componentInstance.listFilter = 'drafts';
    fixture.componentInstance.page = 2;

    fixture.detectChanges();
    await fixture.whenStable();
    await new Promise<void>(resolve => setTimeout(resolve, 0));

    expect(rareBooks.refreshFromServer).toHaveBeenCalledWith({
      search: 'Victor Hugo', status: 'Draft', page: 2, pageSize: 24,
    });
  });
});
