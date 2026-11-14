import {ComponentFixture, TestBed} from '@angular/core/testing';

import {HeartsComponent} from './hearts.component';

describe('HeartsComponent', () => {
  let fixture: ComponentFixture<HeartsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({declarations: [HeartsComponent]}).compileComponents();
    fixture = TestBed.createComponent(HeartsComponent);
    fixture.componentInstance.value = 4;
    fixture.detectChanges();
  });

  it('renders five accessible hearts and labels their score', () => {
    const image = fixture.nativeElement.querySelector('[role="img"]') as HTMLElement;

    expect(image.getAttribute('aria-label')).toBe('4 cœurs sur 5');
    expect(image.querySelectorAll('svg').length).toBe(5);
    expect(image.querySelectorAll('svg.filled').length).toBe(4);
  });
});
