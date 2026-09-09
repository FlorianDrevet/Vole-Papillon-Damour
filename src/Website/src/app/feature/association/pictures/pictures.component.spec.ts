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
    expect(component.photoAlbums.map(album => album.title)).toContain('Bourse aux livres');
    expect(component.photoAlbums.map(album => album.title)).toContain('Maxence · 2004–2010');
    expect(component.videos.length).toBe(14);
    expect(pageText).toContain('La vidéothèque');
    expect(pageText).not.toContain('Albums à venir');
  });
});
