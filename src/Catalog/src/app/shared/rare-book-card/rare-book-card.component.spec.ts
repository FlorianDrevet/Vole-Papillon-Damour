import {CommonModule, registerLocaleData} from '@angular/common';
import {LOCALE_ID} from '@angular/core';
import localeFr from '@angular/common/locales/fr';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {RouterModule} from '@angular/router';

import {CatalogRareBook} from '../../core/catalog.models';
import {CatalogRareBookCardComponent} from './rare-book-card.component';

registerLocaleData(localeFr);

describe('CatalogRareBookCardComponent', () => {
  let fixture: ComponentFixture<CatalogRareBookCardComponent>;

  const book: CatalogRareBook = {
    id: 'rare-1',
    slug: 'les-fables',
    isbn13: null,
    title: 'Les Fables',
    authorMention: 'Jean de La Fontaine',
    publisher: 'Imprimerie royale',
    publicationYear: 1770,
    shelf: 'Éditions anciennes',
    price: 60,
    condition: 'GoodWithFlaws',
    publicDescription: 'Exemplaire illustré.',
    binding: 'Demi-reliure cuir',
    dimensions: null,
    pageCount: 240,
    status: 'Published',
    isSold: true,
    soldAt: '2026-02-08T10:00:00Z',
    photos: [
      {
        id: 'photo-1',
        blobUri: 'https://cdn.example.test/one.jpg',
        blobName: 'one.jpg',
        caption: null,
        position: 0,
        contentType: 'image/jpeg',
        sizeBytes: 100,
        uploadedAt: '2026-09-17T10:00:00Z',
      },
      {
        id: 'photo-2',
        blobUri: 'https://cdn.example.test/two.jpg',
        blobName: 'two.jpg',
        caption: null,
        position: 1,
        contentType: 'image/jpeg',
        sizeBytes: 100,
        uploadedAt: '2026-09-17T10:00:00Z',
      },
    ],
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [CatalogRareBookCardComponent],
      imports: [CommonModule, RouterModule.forRoot([])],
      providers: [{provide: LOCALE_ID, useValue: 'fr-FR'}],
    }).compileComponents();

    fixture = TestBed.createComponent(CatalogRareBookCardComponent);
    fixture.componentInstance.book = book;
    fixture.detectChanges();
  });

  it('shows the evidence details that distinguish a rare-book card', () => {
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('.rare-book-card-photo-count')?.textContent).toContain('2 photos');
    expect(element.querySelector('.rare-book-card-technical-line')?.textContent)
      .toContain('Imprimerie royale · 1770 · Demi-reliure cuir · sans ISBN');
    expect(element.querySelector('.rare-book-card-badge')?.textContent).toContain('Rare');
  });

  it('keeps a sold copy visible with its sale date and crossed price', () => {
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('.rare-book-card--sold')).not.toBeNull();
    expect(element.querySelector('.rare-book-card-price')?.classList).toContain('rare-book-card-price--sold');
    expect(element.querySelector('.rare-book-card-action')?.textContent).toContain('Vendu le 8 février');
  });
});
