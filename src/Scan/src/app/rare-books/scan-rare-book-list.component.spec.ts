import {NO_ERRORS_SCHEMA} from '@angular/core';
import {CommonModule} from '@angular/common';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {FormsModule} from '@angular/forms';
import {RouterTestingModule} from '@angular/router/testing';

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
    rareBooks.refreshFromServer.and.returnValue(Promise.resolve([]));
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
      Promise.resolve([]),
    );

    fixture.detectChanges();
    await fixture.whenStable();
    await new Promise<void>(resolve => setTimeout(resolve, 0));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.rare-error')?.textContent)
      .toContain('Impossible de charger les fiches rares');
    const retry = fixture.nativeElement.querySelector('.rare-retry') as HTMLButtonElement;
    expect(retry).not.toBeNull();

    retry.click();
    await fixture.whenStable();
    await new Promise<void>(resolve => setTimeout(resolve, 0));
    fixture.detectChanges();

    expect(rareBooks.refreshFromServer).toHaveBeenCalledTimes(2);
    expect(fixture.nativeElement.querySelector('.rare-error')).toBeNull();
  });

  it('keeps the add action in the page header', () => {
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.rare-management-header .rare-add'))
      .not.toBeNull();
  });
});
