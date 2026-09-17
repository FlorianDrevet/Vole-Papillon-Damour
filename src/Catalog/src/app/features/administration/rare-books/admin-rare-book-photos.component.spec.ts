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

  it('marks the first photo as the thumbnail and exposes the dedicated container', () => {
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('[data-testid="rare-photo-1"]')).not.toBeNull();
    expect(element.textContent).toContain('Vignette');
    expect(element.textContent).toContain('conteneur « livres-rares »');
    expect(element.querySelector('[data-testid="rare-photo-add"]')).not.toBeNull();
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
