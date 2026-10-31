import {ComponentFixture, TestBed} from '@angular/core/testing';
import {VpdBouleBingoComponent} from './boule-bingo.component';

describe('VpdBouleBingoComponent', () => {
  let fixture: ComponentFixture<VpdBouleBingoComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({imports: [VpdBouleBingoComponent]}).compileComponents();
    fixture = TestBed.createComponent(VpdBouleBingoComponent);
  });

  it('keeps a two-digit number visible as HTML text', () => {
    fixture.componentRef.setInput('num', '03');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.bb-num')?.textContent.trim()).toBe('03');
    expect(fixture.nativeElement.querySelector('[aria-hidden="true"]')).not.toBeNull();
  });

  it('changes the full sphere gradient with the selected color', () => {
    fixture.componentRef.setInput('color', 'bleu');
    fixture.detectChanges();
    const blueStops = Array.from(fixture.nativeElement.querySelectorAll('stop'))
      .map((stop: any) => stop.getAttribute('stop-color'));

    fixture.componentRef.setInput('color', 'orange');
    fixture.detectChanges();
    const orangeStops = Array.from(fixture.nativeElement.querySelectorAll('stop'))
      .map((stop: any) => stop.getAttribute('stop-color'));

    expect(orangeStops.length).toBe(3);
    expect(orangeStops[1]).toBe('#f0801c');
    expect(orangeStops).not.toEqual(blueStops);
  });
});
