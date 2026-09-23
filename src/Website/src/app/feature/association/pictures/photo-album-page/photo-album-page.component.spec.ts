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
    const photoButtons = page.querySelectorAll('[data-photo-trigger]');
    const backLink = page.querySelector('[data-back-to-albums]') as HTMLAnchorElement | null;

    expect(page.querySelector('h1')?.textContent).toContain('2005');
    expect(images.length).toBe(94);
    expect(photoButtons.length).toBe(94);
    expect(backLink).not.toBeNull();
    expect(backLink?.getAttribute('href')).toBe('/association/photos');
    expect((page.querySelector('[data-photo-lightbox]') as HTMLDialogElement).open).toBeFalse();
  });

  it('opens a photo, navigates with the keyboard, and restores focus when closed', async () => {
    const page = fixture.nativeElement as HTMLElement;
    const trigger = page.querySelector('[data-photo-trigger]') as HTMLButtonElement;
    const dialog = page.querySelector('[data-photo-lightbox]') as HTMLDialogElement;

    trigger.click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(dialog.open).toBeTrue();
    expect(document.activeElement).toBe(page.querySelector('[data-photo-close]'));
    expect(page.querySelector('[data-photo-count]')?.textContent?.trim()).toBe('1 / 94');

    dialog.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
    fixture.detectChanges();
    expect(page.querySelector('[data-photo-count]')?.textContent?.trim()).toBe('2 / 94');

    dialog.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();

    expect(dialog.open).toBeFalse();
    expect(document.activeElement).toBe(trigger);
  });

  it('releases the page scroll lock if the album route is destroyed while open', () => {
    const page = fixture.nativeElement as HTMLElement;
    const trigger = page.querySelector('[data-photo-trigger]') as HTMLButtonElement;

    trigger.click();
    fixture.detectChanges();

    expect(document.body.classList.contains('no-scroll')).toBeTrue();

    fixture.destroy();

    expect(document.body.classList.contains('no-scroll')).toBeFalse();
  });
});
