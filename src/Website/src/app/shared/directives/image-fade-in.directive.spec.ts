import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ImageFadeInDirective } from './image-fade-in.directive';

@Component({
  template: '<img appImageFadeIn src="data:image/gif;base64,R0lGODlhAQABAAD/ACwAAAAAAQABAAACADs=" alt="">',
  standalone: false,
})
class ImageFadeHostComponent {}

describe('ImageFadeInDirective', () => {
  let fixture: ComponentFixture<ImageFadeHostComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ImageFadeHostComponent, ImageFadeInDirective],
    }).compileComponents();
    fixture = TestBed.createComponent(ImageFadeHostComponent);
  });

  it('marks an image visible when it finishes loading', () => {
    fixture.detectChanges();
    const image = fixture.nativeElement.querySelector('img') as HTMLImageElement;

    image.dispatchEvent(new Event('load'));

    expect(image.getAttribute('data-image-state')).toBe('visible');
  });
});
