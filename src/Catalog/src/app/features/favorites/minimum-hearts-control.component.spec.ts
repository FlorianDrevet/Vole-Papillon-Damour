import {ComponentFixture, TestBed} from '@angular/core/testing';

import {MinimumHeartsControlComponent} from './minimum-hearts-control.component';

describe('MinimumHeartsControlComponent', () => {
  let fixture: ComponentFixture<MinimumHeartsControlComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [MinimumHeartsControlComponent],
    }).compileComponents();
    fixture = TestBed.createComponent(MinimumHeartsControlComponent);
    fixture.detectChanges();
  });

  it('exposes four pressed-state choices with readable and compact labels', () => {
    const buttons = fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>;
    expect(buttons.length).toBe(4);
    expect(buttons[2].getAttribute('aria-label')).toBe('3 cœurs et +');
    expect(buttons[2].getAttribute('aria-pressed')).toBe('true');
    expect(buttons[3].querySelector('.minimum-hearts-mobile')?.textContent).toBe('4+');
  });

  it('emits the selected minimum', () => {
    const changed = jasmine.createSpy('minimumChanged');
    fixture.componentInstance.minimumChange.subscribe(changed);

    (fixture.nativeElement.querySelector('[data-zone="E2-exigence-4"]') as HTMLButtonElement).click();

    expect(changed).toHaveBeenCalledOnceWith(4);
  });
});
