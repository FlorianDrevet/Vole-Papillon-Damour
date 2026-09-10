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

  it('should split the photo catalog into Maxence years and events', () => {
    const pageText = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();
    const categories = fixture.nativeElement.querySelectorAll('[data-album-category]');

    expect(component.photoAlbums.length).toBe(26);
    expect(categories.length).toBe(2);
    expect(categories[0].querySelector('h2').textContent).toContain('Vie de Maxence');
    expect(categories[0].querySelectorAll('[data-album-card]').length).toBe(23);
    expect(categories[1].querySelector('h2').textContent).toContain('Événements');
    expect(categories[1].querySelectorAll('[data-album-card]').length).toBe(3);
    expect(component.photoAlbums.map(album => album.title)).toContain('2004');
    expect(component.photoAlbums.map(album => album.title)).toContain('2026');
    expect(component.photoAlbums.map(album => album.title)).toContain('Anniversaire 20 ans Maxence');
    const albumsBySlug = new Map(component.photoAlbums.map(album => [album.slug, album]));
    expect(albumsBySlug.get('maxence-2005')?.photos.length).toBe(94);
    expect(albumsBySlug.get('maxence-2026')?.photos.length).toBe(31);
    expect(albumsBySlug.get('celebrites')?.photos.length).toBe(15);
    expect(albumsBySlug.get('anniversaire-20-ans-maxence')?.photos.length).toBe(88);
    expect(component.videos.length).toBe(14);
    expect(fixture.nativeElement.querySelectorAll('[data-album-card]').length).toBe(component.photoAlbums.length);
    expect(pageText).toContain('La vidéothèque');
    expect(pageText).not.toContain('Albums à venir');
  });

  it('should navigate to an album page instead of opening a dialog', () => {
    const cards = fixture.nativeElement.querySelectorAll('[data-album-card]');

    expect(cards.length).toBe(component.photoAlbums.length);
    const firstLink = cards[0] as HTMLAnchorElement;

    expect(firstLink.getAttribute('href')).toBe('/association/photos/maxence-2004');
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[aria-haspopup="dialog"]')).toBeNull();
  });

  it('should keep the history banner after the video catalog', () => {
    const sections = Array.from(fixture.nativeElement.querySelectorAll('section[data-section]'))
      .map(section => (section as Element).getAttribute('data-section'));

    expect(sections.indexOf('videos')).toBeLessThan(sections.indexOf('history'));
  });

  it('should describe the association film and Michael Jones clip accurately', () => {
    const descriptions = new Map(component.videos.map(video => [video.title, video.description]));

    expect(descriptions.get('Vole, Papillon d’amour')).toBe('C’est le combat de Maxence, pas celui de l’association.');
    expect(descriptions.get('Le clip')).toBe('Une chanson et un clip avec la participation de Michael Jones.');
  });

  it('should align mixed video cards within each grid row', () => {
    const videoGrid = fixture.nativeElement.querySelector('[data-video-grid]');

    expect(videoGrid.classList).toContain('items-stretch');
    expect(videoGrid.querySelectorAll('[data-video-card]').length).toBe(component.videos.length);
    expect(videoGrid.querySelector('[data-video-card]')?.classList).toContain('h-full');
    expect(videoGrid.querySelector('[data-video-copy]')?.classList).toContain('flex-1');
  });
});
