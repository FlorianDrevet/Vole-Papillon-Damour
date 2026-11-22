import {LabelJob, validateLabelJob} from './label-printer';
import {describeConnectionError, SupvanLabelPrinter} from './supvan/supvan-printer';

const ISBN_JOB: LabelJob = {code: '9782070408504', price: null, copies: 1};

function namedError(name: string, message = 'x'): Error {
  const error = new Error(message);
  error.name = name;
  return error;
}

describe('SupvanLabelPrinter', () => {
  beforeEach(() => localStorage.removeItem('vpd.labelPrinter.density'));
  afterEach(() => localStorage.removeItem('vpd.labelPrinter.density'));

  it('démarre déconnectée, sans rouleau', () => {
    const printer = new SupvanLabelPrinter();

    expect(printer.status()).toBe('disconnected');
    expect(printer.roll).toBeNull();
  });

  it('refuse d\'imprimer tant qu\'elle n\'est pas connectée, et le trace', async () => {
    const events: Array<{name: string; properties: {[key: string]: string}}> = [];
    const printer = new SupvanLabelPrinter({trackEvent: (name, properties) => events.push({name, properties})});

    await expectAsync(printer.print(ISBN_JOB)).toBeRejectedWithError(/non connectée/);

    expect(events.length).toBe(1);
    expect(events[0].name).toBe('label_printer_print');
    expect(events[0].properties['ok']).toBe('false');
    expect(events[0].properties['code']).toBe(ISBN_JOB.code);
    expect(printer.journal).toContain('ERREUR d\'impression');
  });

  it('rejette un code qui n\'est pas un EAN-13 valide avant toute tentative', async () => {
    const printer = new SupvanLabelPrinter();

    await expectAsync(printer.print({...ISBN_JOB, code: '1234'})).toBeRejected();
  });

  it('mémorise la noirceur entre 1 et 15', () => {
    const printer = new SupvanLabelPrinter();

    printer.density = 99;
    expect(printer.density).toBe(15);
    printer.density = 0;
    expect(printer.density).toBe(1);
    printer.density = 7;

    expect(new SupvanLabelPrinter().density).toBe(7);
  });

  it('prévient ses abonnés quand le journal change, jusqu\'au désabonnement', () => {
    const printer = new SupvanLabelPrinter();
    let calls = 0;
    const unsubscribe = printer.subscribe(() => { calls += 1; });

    printer.log('une ligne');
    expect(calls).toBe(1);
    unsubscribe();
    printer.log('une autre');

    expect(calls).toBe(1);
  });
});

describe('describeConnectionError', () => {
  it('traduit les refus et pannes Bluetooth courants', () => {
    expect(describeConnectionError(namedError('NotFoundError'))).toContain('Aucune imprimante choisie');
    expect(describeConnectionError(namedError('NetworkError'))).toContain('Connexion impossible');
    expect(describeConnectionError(namedError('NotAllowedError'))).toContain('refusé');
    expect(describeConnectionError(new Error('autre'))).toBe('autre');
  });
});

describe('validateLabelJob', () => {
  it('accepte entre 1 et 50 exemplaires', () => {
    expect(validateLabelJob({...ISBN_JOB, copies: 1})).toBeNull();
    expect(validateLabelJob({...ISBN_JOB, copies: 50})).toBeNull();
  });

  it('refuse 0 exemplaire ou plus de 50', () => {
    expect(validateLabelJob({...ISBN_JOB, copies: 0})).not.toBeNull();
    expect(validateLabelJob({...ISBN_JOB, copies: 51})).not.toBeNull();
  });
});
