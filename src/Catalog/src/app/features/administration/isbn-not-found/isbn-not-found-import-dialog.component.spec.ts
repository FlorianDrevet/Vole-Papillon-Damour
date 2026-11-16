import {provideZonelessChangeDetection} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {of, Subject, throwError} from 'rxjs';

import {CatalogAdminApiService} from '../../../core/catalog-admin-api.service';
import {
  CatalogAdminIsbnNotFoundImportPreview,
  CatalogAdminIsbnNotFoundImportResult,
} from '../../../core/catalog.models';
import {IsbnNotFoundImportDialogComponent} from './isbn-not-found-import-dialog.component';

describe('IsbnNotFoundImportDialogComponent', () => {
  let fixture: ComponentFixture<IsbnNotFoundImportDialogComponent>;
  let api: jasmine.SpyObj<CatalogAdminApiService>;

  const preview: CatalogAdminIsbnNotFoundImportPreview = {
    dataRowCount: 8,
    readyCount: 4,
    withoutAuthorCount: 1,
    ignoredCount: 2,
    rejectedCount: 2,
    readyIsbns: ['9782070368228', '9782253004226', '9782070413119', '9782266071529'],
    rejections: [
      {rowNumber: 4, isbn: '9782070368229', reason: 'ISBN invalide'},
      {rowNumber: 7, isbn: '9782290349281', reason: 'Titre manquant'},
    ],
  };

  const result: CatalogAdminIsbnNotFoundImportResult = {
    dataRowCount: 8,
    importedCount: 4,
    withoutAuthorCount: 1,
    markedAbsentCount: 4,
    rejectedCount: 2,
    rejections: preview.rejections,
  };

  beforeEach(async () => {
    api = jasmine.createSpyObj<CatalogAdminApiService>('CatalogAdminApiService', [
      'previewIsbnNotFoundImport', 'importIsbnNotFound',
    ]);
    api.previewIsbnNotFoundImport.and.returnValue(of(preview));
    api.importIsbnNotFound.and.returnValue(of(result));

    await TestBed.configureTestingModule({
      declarations: [IsbnNotFoundImportDialogComponent],
      providers: [
        provideZonelessChangeDetection(),
        {provide: CatalogAdminApiService, useValue: api},
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(IsbnNotFoundImportDialogComponent);
    fixture.componentRef.setInput('accessToken', 'admin-token');
  });

  it('previews the selected file without saving and displays the server summary', async () => {
    fixture.detectChanges();
    const file = new File(['csv'], 'livres.csv', {type: 'text/csv'});
    await fixture.componentInstance.selectFile(file);
    fixture.detectChanges();

    expect(api.previewIsbnNotFoundImport).toHaveBeenCalledOnceWith('admin-token', file);
    expect(api.importIsbnNotFound).not.toHaveBeenCalled();
    const root = fixture.nativeElement as HTMLElement;
    expect(root.textContent).toContain('Vérifier le fichier avant d’enregistrer');
    expect(root.textContent).toContain('livres.csv');
    expect(root.textContent).toContain('8 lignes lues');
    expect(root.textContent).toContain('4 fiches prêtes');
    expect(root.textContent).toContain('Sans auteur');
    expect(root.textContent).toContain('2 lignes rejetées');
    expect(root.textContent).toContain('ISBN invalide');
  });

  it('keeps the save button disabled when no fiche is ready', async () => {
    api.previewIsbnNotFoundImport.and.returnValue(of({...preview, readyCount: 0, withoutAuthorCount: 0, ignoredCount: 8, rejectedCount: 0, readyIsbns: [], rejections: []}));
    fixture.detectChanges();
    await fixture.componentInstance.selectFile(new File(['csv'], 'vide.csv', {type: 'text/csv'}));
    fixture.detectChanges();

    const save = fixture.nativeElement.querySelector('[data-testid="isbn-import-save"]') as HTMLButtonElement;
    expect(save.textContent?.trim()).toBe('Enregistrer 0 fiche');
    expect(save.disabled).toBeTrue();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Aucune ligne à enregistrer');
  });

  it('shows a rejected-file error without offering an import action', async () => {
    api.previewIsbnNotFoundImport.and.returnValue(throwError(() => ({
      error: {detail: 'Colonnes manquantes : « Titre », « Couverture_URL ». Colonne inconnue : « Titre_livre ».'},
    })));
    fixture.detectChanges();
    await fixture.componentInstance.selectFile(new File(['bad'], 'malforme.csv', {type: 'text/csv'}));
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('[role="alert"]')?.textContent).toContain('Colonnes manquantes');
    expect(root.querySelector('[data-testid="isbn-import-choose-another"]')).not.toBeNull();
    expect(root.querySelector('[data-testid="isbn-import-save"]')).toBeNull();
    expect(api.importIsbnNotFound).not.toHaveBeenCalled();
  });

  it('shows progress and confirms the server-provided ready ISBNs when saving', async () => {
    const importResponse = new Subject<CatalogAdminIsbnNotFoundImportResult>();
    api.importIsbnNotFound.and.returnValue(importResponse.asObservable());
    fixture.detectChanges();
    const file = new File(['csv'], 'livres.csv', {type: 'text/csv'});
    await fixture.componentInstance.selectFile(file);
    fixture.detectChanges();

    const savePromise = fixture.componentInstance.saveImport();
    fixture.detectChanges();
    expect(api.importIsbnNotFound).toHaveBeenCalledOnceWith('admin-token', file, preview.readyIsbns);
    expect(fixture.nativeElement.querySelector('[role="progressbar"]')).not.toBeNull();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Enregistrement des fiches…');

    importResponse.next(result);
    importResponse.complete();
    await savePromise;
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('4 fiches enregistrées');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('3 marquées « complétées »');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('1 sans auteur');
  });

  it('reveals all rejection details in the completed-import summary', async () => {
    fixture.detectChanges();
    await fixture.componentInstance.selectFile(new File(['csv'], 'livres.csv', {type: 'text/csv'}));
    await fixture.componentInstance.saveImport();
    fixture.detectChanges();

    const details = fixture.nativeElement.querySelector('[data-testid="isbn-import-rejection-details"]') as HTMLButtonElement;
    expect(details?.textContent?.trim()).toBe('voir le détail');
    details.click();
    fixture.detectChanges();

    const rows = [...(fixture.nativeElement as HTMLElement).querySelectorAll('.isbn-import-result-rejections tbody tr')];
    expect(rows.map(row => (row as HTMLTableRowElement).cells[0].textContent?.trim())).toEqual(['4', '7']);
  });

  it('emits close when the administrator cancels the dialog', () => {
    fixture.detectChanges();
    const closed = jasmine.createSpy('closed');
    fixture.componentInstance.closed.subscribe(closed);

    (fixture.nativeElement.querySelector('[data-testid="isbn-import-cancel"]') as HTMLButtonElement).click();

    expect(closed).toHaveBeenCalledOnceWith(undefined);
  });
});
