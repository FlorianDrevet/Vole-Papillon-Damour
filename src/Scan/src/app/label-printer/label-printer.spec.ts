import {LabelJob, SimulatedLabelPrinter, validateLabelJob} from './label-printer';

const ISBN_JOB: LabelJob = {code: '9782070408504', price: null, copies: 1};

describe('SimulatedLabelPrinter', () => {
  it('se déclare simulée et démarre déconnectée', () => {
    const printer = new SimulatedLabelPrinter();

    expect(printer.simulated).toBeTrue();
    expect(printer.status()).toBe('disconnected');
  });

  it('refuse d\'imprimer tant qu\'elle n\'est pas connectée', async () => {
    const printer = new SimulatedLabelPrinter();

    await expectAsync(printer.print(ISBN_JOB)).toBeRejectedWithError('Imprimante non connectée.');
  });

  it('accepte une impression une fois connectée', async () => {
    const printer = new SimulatedLabelPrinter();
    await printer.connect();

    expect(printer.status()).toBe('connected');
    await expectAsync(printer.print(ISBN_JOB)).toBeResolved();
  });

  it('revient à déconnectée après disconnect', async () => {
    const printer = new SimulatedLabelPrinter();
    await printer.connect();
    printer.disconnect();

    expect(printer.status()).toBe('disconnected');
  });

  it('rejette un code qui n\'est pas un EAN-13 valide', async () => {
    const printer = new SimulatedLabelPrinter();
    await printer.connect();

    await expectAsync(printer.print({...ISBN_JOB, code: '1234'})).toBeRejected();
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
