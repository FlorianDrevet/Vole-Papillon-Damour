import {ComponentFixture, TestBed} from '@angular/core/testing';
import {VpdPapillonComponent} from './papillon.component';

describe('VpdPapillonComponent', () => {
  let fixture: ComponentFixture<VpdPapillonComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({imports: [VpdPapillonComponent]}).compileComponents();
    fixture = TestBed.createComponent(VpdPapillonComponent);
  });

  it('renders the decorative butterfly as hidden from assistive technology', () => {
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[aria-hidden="true"]')).not.toBeNull();
  });

  it('switches to the posed wing state when asked', () => {
    fixture.detectChanges();
    const papillon = fixture.nativeElement.querySelector('.pa') as HTMLElement;
    expect(papillon.classList.contains('pa-pose')).toBeFalse();

    fixture.componentRef.setInput('mood', 'pose');
    fixture.detectChanges();

    expect(papillon.classList.contains('pa-pose')).toBeTrue();
  });
});
