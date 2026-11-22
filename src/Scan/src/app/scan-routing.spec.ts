import {scanPrintGuard, scanRoutes} from './scan-routing';

describe('scan routing', () => {
  it('exposes the unified shell, operating routes, and private statistics route', () => {
    const shell = scanRoutes.find(route => route.path === '');
    const childPaths = shell?.children?.map(route => route.path)
      .filter(path => path !== '**') ?? [];

    expect(childPaths).toEqual(jasmine.arrayWithExactContents([
      '',
      'accueil',
      'recherche-livre',
      'reprise',
      'tri/mode',
      'tri',
      'tri/livres',
      'tri/fin',
      'caisse',
      'statistiques',
      'livres-rares',
      'livres-rares/sans-isbn',
      'sans-isbn',
      'imprimante',
      'imprimante/diagnostic',
      'etiquette',
      'livres-rares/nouveau',
      'livres-rares/:id',
    ]));
    expect(childPaths).not.toContain('consulter');
  });

  it('keeps role guards on the tri and caisse routes', () => {
    const shell = scanRoutes.find(route => route.path === '');
    const children = shell?.children ?? [];

    expect(children.find(route => route.path === 'tri')?.canActivate?.length).toBe(1);
    expect(children.find(route => route.path === 'tri/mode')?.canActivate?.length).toBe(1);
    expect(children.find(route => route.path === 'tri/livres')?.canActivate?.length).toBe(1);
    expect(children.find(route => route.path === 'caisse')?.canActivate?.length).toBe(1);
    expect(children.find(route => route.path === 'sans-isbn')?.canActivate).toEqual([scanPrintGuard]);
    expect(children.find(route => route.path === 'recherche-livre')?.loadComponent).toEqual(jasmine.any(Function));
    expect(children.find(route => route.path === 'tri/fin')?.canActivate?.length).toBe(2);
    expect(children.find(route => route.path === 'livres-rares')?.canActivate?.length).toBe(1);
    expect(children.find(route => route.path === 'livres-rares/sans-isbn')?.canActivate?.length).toBe(1);
    expect(children.find(route => route.path === 'livres-rares/nouveau')?.canActivate?.length).toBe(1);
    expect(children.find(route => route.path === 'livres-rares/:id')?.canActivate?.length).toBe(1);
  });
});
