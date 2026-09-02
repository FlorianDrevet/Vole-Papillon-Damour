import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ActionCardComponent } from './action-card.component';
import { PhotoPlaceholderComponent } from '../photo-placeholder/photo-placeholder.component';

describe('ActionCardComponent', () => {
  let fixture: ComponentFixture<ActionCardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ActionCardComponent, PhotoPlaceholderComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(ActionCardComponent);
    fixture.componentRef.setInput('meta', 'ACTION');
    fixture.componentRef.setInput('title', 'Une aide concrète');
  });

  it('renders the supplied photo and alternative text', () => {
    fixture.componentRef.setInput('photoSrc', 'images/actions/aide.jpg');
    fixture.componentRef.setInput('photoAlt', 'Une aide rendue possible');

    fixture.detectChanges();

    const image = fixture.nativeElement.querySelector('img') as HTMLImageElement;

    expect(image.getAttribute('src')).toBe('images/actions/aide.jpg');
    expect(image.alt).toBe('Une aide rendue possible');
    expect(fixture.nativeElement.querySelector('app-photo-placeholder')).toBeNull();
  });

  it('keeps the photo placeholder when no photo is supplied', () => {
    fixture.componentRef.setInput('photoLabel', 'PHOTO À VENIR');

    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('img')).toBeNull();
    expect(fixture.nativeElement.querySelector('app-photo-placeholder')).not.toBeNull();
  });
});
