import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatNativeDateModule } from '@angular/material/core';
import { MatSnackBar } from '@angular/material/snack-bar';

import { SharedModule } from '../../../shared.module';
import { VpdEventsFacadeService } from '../../../facades/vpd-events.facade.service';
import { VpdEventEnum } from '../../../enums/vpdEvent.enum';
import { MyDate } from '../../../extensions/MyDate';
import { VpdEventModel } from '../../../models/vpdEvent.model';
import { CreateUpdateEventDialogComponent } from './create-update-event-dialog.component';

describe('CreateUpdateEventDialogComponent', () => {
  let fixture: ComponentFixture<CreateUpdateEventDialogComponent>;
  let component: CreateUpdateEventDialogComponent;

  const event: VpdEventModel = {
    id: 'event-1',
    eventType: VpdEventEnum.Books,
    name: 'Bourse aux livres',
    description: 'Description',
    dateStart: new MyDate('2026-10-05T04:00:00.000Z'),
    dateEnd: new MyDate('2026-10-11T04:00:00.000Z'),
    hourOpenDoors: new MyDate('2026-10-05T14:00:00.000Z'),
    hourCloseDoors: new MyDate('2026-10-05T18:00:00.000Z'),
    urlImageMap: null,
    urlRegistration: null,
    urlImage: 'https://cdn.example.test/books.jpg',
    city: 'Andrézieux-Bouthéon',
    road: 'rue Pierre-Georges Latécoère',
    cityCode: '42160',
    roadNumber: '482',
    parties: [],
    currentPartieIndex: 0,
    bingoHasBeenWon: false,
    bingoNumeros: [],
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SharedModule, ReactiveFormsModule, MatNativeDateModule],
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: event },
        { provide: MatDialogRef, useValue: { close: jasmine.createSpy('close') } },
        { provide: MatSnackBar, useValue: { open: jasmine.createSpy('open') } },
        { provide: VpdEventsFacadeService, useValue: {} },
      ],
    })
      .compileComponents();

    fixture = TestBed.createComponent(CreateUpdateEventDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('hydrates date and time pickers with the values displayed by the API', () => {
    const dateStart = component.newEventForm.get('dateStart')?.value as Date;
    const hourOpenDoors = component.newEventForm.get('hourOpenDoors')?.value as Date;
    const hourCloseDoors = component.newEventForm.get('hourCloseDoors')?.value as Date;

    expect(dateStart.getFullYear()).toBe(2026);
    expect(dateStart.getMonth()).toBe(9);
    expect(dateStart.getDate()).toBe(5);
    expect(dateStart.getHours()).toBe(0);
    expect(hourOpenDoors.getHours()).toBe(14);
    expect(hourCloseDoors.getHours()).toBe(18);
  });

  it('shows the live theme selector only for a loto', () => {
    const label = 'Thème du tableau en direct';
    expect(fixture.nativeElement.textContent).not.toContain(label);

    component.newEventForm.get('eventType')?.setValue(VpdEventEnum.Bingo);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain(label);

    component.newEventForm.get('eventType')?.setValue(VpdEventEnum.Books);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain(label);
  });

  it('sends the selected Halloween theme only for a loto', () => {
    expect(component.newEventForm.contains('liveTheme')).toBeTrue();
    expect(component.newEventForm.get('liveTheme')?.value).toBe('Classic');
    component.newEventForm.get('eventType')?.setValue(VpdEventEnum.Bingo);
    component.newEventForm.get('liveTheme')?.setValue('Halloween');
    const consoleLog = spyOn(console, 'log');

    const lotoFormData = (component as any).createFormData() as FormData;

    expect(lotoFormData.get('LiveTheme')).toBe('Halloween');

    component.newEventForm.get('eventType')?.setValue(VpdEventEnum.Books);
    const booksFormData = (component as any).createFormData() as FormData;

    expect(booksFormData.has('LiveTheme')).toBeFalse();
    consoleLog.and.stub();
  });
});
