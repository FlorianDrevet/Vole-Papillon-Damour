import {ComponentFixture, TestBed} from '@angular/core/testing';

import {
  AdminRareBookPhotosComponent,
  type RareBookPhotoAction,
} from './admin-rare-book-photos.component';

describe('AdminRareBookPhotosComponent', () => {
  let fixture: ComponentFixture<AdminRareBookPhotosComponent>;
  let component: AdminRareBookPhotosComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [AdminRareBookPhotosComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(AdminRareBookPhotosComponent);
    component = fixture.componentInstance;
    component.photos = [{
      id: 'photo-1',
      blobUri: 'https://blob.example/one.jpg',
      blobName: 'rare-book-id/photo-1.jpg',
      caption: 'Couverture',
      position: 0,
      contentType: 'image/jpeg',
      sizeBytes: 1_024,
      uploadedAt: '2026-09-16T10:00:00Z',
      uploadedBy: 'volunteer-id',
    }, {
      id: 'photo-2',
      blobUri: 'https://blob.example/two.jpg',
      blobName: 'rare-book-id/photo-2.jpg',
      caption: null,
      position: 1,
      contentType: 'image/jpeg',
      sizeBytes: 2_048,
      uploadedAt: '2026-09-16T10:01:00Z',
      uploadedBy: 'volunteer-id',
    }];
    fixture.detectChanges();
  });

  it('marks the first photo as the thumbnail and shows only the photo count', () => {
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('[data-testid="rare-photo-1"]')).not.toBeNull();
    expect(element.textContent).toContain('Vignette');
    expect(element.querySelector('.rare-photo-count')?.textContent).toContain('2 photos');
    expect(element.textContent).not.toContain('conteneur « livres-rares »');
    expect(element.textContent).not.toContain('3 Ko');
    expect(element.querySelector('[data-testid="rare-photo-add"]')).not.toBeNull();
  });

  it('does not show storage metadata for an empty photo gallery', () => {
    fixture.componentRef.setInput('photos', []);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('0 o');
    expect(text).not.toContain('conteneur « livres-rares »');
  });

  it('shows an immediate preview while the selected photo is being saved', () => {
    const file = new File(['photo'], 'photo.jpg', {type: 'image/jpeg'});
    const actions: RareBookPhotoAction[] = [];
    component.action.subscribe(action => actions.push(action));
    const input = fixture.nativeElement.querySelector('input[type="file"]') as HTMLInputElement;
    Object.defineProperty(input, 'files', {configurable: true, value: [file]});
    input.dispatchEvent(new Event('change'));
    expect(component.pendingPhotos.length).toBe(1);
    fixture.detectChanges();

    const preview = fixture.nativeElement.querySelector('[data-testid="rare-photo-pending-1"] img') as HTMLImageElement;
    expect(preview).not.toBeNull();
    expect(preview.src).toContain('blob:');
    expect(actions).toEqual([{kind: 'add', file, caption: ''}]);
  });

  it('replaces the preview when the saved photo arrives from the server', () => {
    const file = new File(['photo'], 'photo.jpg', {type: 'image/jpeg'});
    const input = fixture.nativeElement.querySelector('input[type="file"]') as HTMLInputElement;
    Object.defineProperty(input, 'files', {configurable: true, value: [file]});
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    fixture.componentRef.setInput('photos', [...component.photos, {
      ...component.photos[0],
      id: 'photo-3',
      blobUri: 'https://blob.example/three.jpg',
      position: 2,
    }]);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="rare-photo-pending-1"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="rare-photo-3"] img')?.getAttribute('src'))
      .toBe('https://blob.example/three.jpg');
  });

  it('emits add, delete and accessible reorder actions', () => {
    const actions: RareBookPhotoAction[] = [];
    component.action.subscribe(action => actions.push(action));
    component.selectPhoto('photo-2');
    component.moveSelected(-1);
    component.deleteSelected();

    expect(actions).toEqual([
      {kind: 'reorder', photoIds: ['photo-2', 'photo-1']},
      {kind: 'delete', photoId: 'photo-2'},
    ]);
  });

  it('rejects a non-image file before emitting an upload', () => {
    const action = jasmine.createSpy('action');
    component.action.subscribe(action);
    component.onFileSelected({target: {files: [new File(['text'], 'notes.txt', {type: 'text/plain'})]}} as unknown as Event);

    expect(action).not.toHaveBeenCalled();
    expect(component.uploadError()).toContain('JPEG, WebP ou PNG');
  });
});
