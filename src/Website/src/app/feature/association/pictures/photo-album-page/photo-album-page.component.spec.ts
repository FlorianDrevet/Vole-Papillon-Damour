import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, RouterLink, convertToParamMap, provideRouter } from '@angular/router';

import { PhotoAlbumPageComponent } from './photo-album-page.component';

describe('PhotoAlbumPageComponent', () => {
  let fixture: ComponentFixture<PhotoAlbumPageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [PhotoAlbumPageComponent],
      imports: [RouterLink],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ albumSlug: 'maxence-2005' }) } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PhotoAlbumPageComponent);
    fixture.detectChanges();
  });

  it('should render every photo of the selected album with a return link', () => {
    const page = fixture.nativeElement as HTMLElement;
    const images = page.querySelectorAll('[data-photo-card] img');
    const backLink = page.querySelector('[data-back-to-albums]') as HTMLAnchorElement | null;

    expect(page.querySelector('h1')?.textContent).toContain('2005');
    expect(images.length).toBe(94);
    expect(backLink).not.toBeNull();
    expect(backLink?.getAttribute('href')).toBe('/association/photos');
    expect(page.querySelector('[role="dialog"]')).toBeNull();
  });
});
