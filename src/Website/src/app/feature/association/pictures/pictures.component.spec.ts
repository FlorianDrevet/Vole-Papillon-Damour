import { NgOptimizedImage } from '@angular/common';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterLink, provideRouter } from '@angular/router';

import { SharedModule } from '../../../shared/shared.module';

import { PicturesComponent } from './pictures.component';

describe('PicturesComponent', () => {
  let component: PicturesComponent;
  let fixture: ComponentFixture<PicturesComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [PicturesComponent],
      // Le gabarit s'appuie sur les composants du design system (bouton pilule,
      // encart photo) et sur des liens de navigation : sans eux, Angular ne
      // reconnaît pas les balises et le rendu échoue.
      imports: [SharedModule, NgOptimizedImage, RouterLink],
      providers: [provideRouter([])]
    })
    .compileComponents();

    fixture = TestBed.createComponent(PicturesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should organise the new photo albums and videos', () => {
    const pageText = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(component.photoAlbums.length).toBe(6);
    expect(component.photoAlbums.at(-1)?.title).toBe('Bourse aux livres');
    expect(component.photoAlbums.map(album => album.title)).toContain('Maxence · 2004–2010');
    expect(component.videos.length).toBe(14);
    expect(fixture.nativeElement.querySelectorAll('[data-album-card]').length).toBe(component.photoAlbums.length);
    expect(pageText).toContain('La vidéothèque');
    expect(pageText).not.toContain('Albums à venir');
  });

  it('should open and close an album from the catalog card', () => {
    const cards = fixture.nativeElement.querySelectorAll('[data-album-card]');

    expect(cards.length).toBe(component.photoAlbums.length);

    cards[0].dispatchEvent(new Event('click'));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[role="dialog"] h2').textContent)
      .toContain(component.photoAlbums[0].title);

    fixture.nativeElement.querySelector('[role="dialog"] button').click();
    cards[1].dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role="dialog"] h2').textContent)
      .toContain(component.photoAlbums[1].title);

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeNull();
  });

  it('should keep the history banner after the video catalog', () => {
    const sections = Array.from(fixture.nativeElement.querySelectorAll('section[data-section]'))
      .map(section => (section as Element).getAttribute('data-section'));

    expect(sections.indexOf('videos')).toBeLessThan(sections.indexOf('history'));
  });

  it('should prevent mixed video cards from stretching each other', () => {
    const videoGrid = fixture.nativeElement.querySelector('[data-video-grid]');

    expect(videoGrid.classList).toContain('items-start');
    expect(videoGrid.querySelectorAll('[data-video-card]').length).toBe(component.videos.length);
  });
});
