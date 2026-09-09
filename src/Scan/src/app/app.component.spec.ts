import {CommonModule} from '@angular/common';
import {Component} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {RouterTestingModule} from '@angular/router/testing';

import {AppComponent} from './app.component';

@Component({
  selector: 'app-scan-shell',
  template: '<div class="shell-stub"></div>',
  standalone: false,
})
class ShellStubComponent {}

@Component({
  selector: 'app-scan-diagnostic',
  template: '<div class="diagnostic-stub"></div>',
  standalone: false,
})
class DiagnosticStubComponent {}

describe('AppComponent', () => {
  let fixture: ComponentFixture<AppComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [
        AppComponent,
        ShellStubComponent,
        DiagnosticStubComponent,
      ],
      imports: [CommonModule, RouterTestingModule.withRoutes([
        {path: '', component: ShellStubComponent},
      ])],
    }).compileComponents();

    fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
  });

  it('mounts the routed shell for the authenticated surface', () => {
    expect(fixture.nativeElement.querySelector('router-outlet')).not.toBeNull();
  });

  it('leaves authorization decisions to the routed shell', () => {
    expect(fixture.nativeElement.querySelector('.scanner-stub')).toBeNull();
  });

  it('keeps the diagnostic surface available when authentication is not authorized', () => {
    fixture.destroy();
    fixture = TestBed.createComponent(AppComponent);
    fixture.componentInstance.diagnosticRequested = true;
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.diagnostic-stub')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.shell-stub')).toBeNull();
  });

});
