import {scanRoutes} from './scan-routing';

describe('scan routing', () => {
  it('exposes the unified shell and the seven operating routes', () => {
    const shell = scanRoutes.find(route => route.path === '');
    const childPaths = shell?.children?.map(route => route.path)
      .filter(path => path !== '**') ?? [];

    expect(childPaths).toEqual(jasmine.arrayWithExactContents([
      '',
      'accueil',
      'reprise',
      'tri/mode',
      'tri',
      'tri/fin',
      'caisse',
      'consulter',
    ]));
  });

  it('keeps role guards on the tri and caisse routes', () => {
    const shell = scanRoutes.find(route => route.path === '');
    const children = shell?.children ?? [];

    expect(children.find(route => route.path === 'tri')?.canActivate?.length).toBe(1);
    expect(children.find(route => route.path === 'tri/mode')?.canActivate?.length).toBe(1);
    expect(children.find(route => route.path === 'caisse')?.canActivate?.length).toBe(1);
    expect(children.find(route => route.path === 'tri/fin')?.canActivate?.length).toBe(2);
  });
});
