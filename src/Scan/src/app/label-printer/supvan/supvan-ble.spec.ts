import {BlePipe} from './supvan-printer';

// Faux Web Bluetooth : un seul service exposé, avec l'UUID tel que le navigateur le renvoie.
// Bluefy (iPhone) renvoie les UUID en MAJUSCULES, Chrome Android en minuscules (journal du 22/11/2026).
function fakeBluetooth(serviceUuid: string) {
  const characteristic = {
    startNotifications: async () => characteristic,
    addEventListener: () => undefined,
    writeValueWithResponse: async () => undefined,
    writeValue: async () => undefined,
  };
  const requested: string[] = [];
  const service = {
    uuid: serviceUuid,
    getCharacteristic: async (uuid: string) => {
      requested.push(uuid);
      return characteristic;
    },
  };
  const server = {getPrimaryServices: async () => [service], disconnect: jasmine.createSpy('disconnect')};
  const device = {name: 'T0205C2608287577', gatt: {connect: async () => server}, addEventListener: () => undefined};
  return {bluetooth: {requestDevice: async () => device}, server, requested};
}

describe('BlePipe — reconnaissance du service Supvan', () => {
  const E0FF = '0000e0ff-3c17-d293-8e48-14fe2e4da212';
  let lines: string[];

  beforeEach(() => {
    lines = [];
  });

  afterEach(() => {
    delete (navigator as unknown as {bluetooth?: unknown}).bluetooth;
  });

  function install(serviceUuid: string) {
    const fake = fakeBluetooth(serviceUuid);
    Object.defineProperty(navigator, 'bluetooth', {value: fake.bluetooth, configurable: true});
    return fake;
  }

  it('reconnaît le service quand le navigateur renvoie l\'UUID en minuscules (Chrome Android)', async () => {
    const fake = install(E0FF);

    await new BlePipe(line => lines.push(line)).open();

    expect(fake.server.disconnect).not.toHaveBeenCalled();
    expect(lines.join('\n')).toContain(`Service Supvan utilisé : ${E0FF}`);
  });

  it('reconnaît le service quand le navigateur renvoie l\'UUID en majuscules (Bluefy, iPhone)', async () => {
    const fake = install(E0FF.toUpperCase());

    await new BlePipe(line => lines.push(line)).open();

    expect(fake.server.disconnect).not.toHaveBeenCalled();
    expect(lines.join('\n')).toContain(`Service Supvan utilisé : ${E0FF}`);
  });

  it('refuse toujours un appareil dont aucun service n\'est connu', async () => {
    const fake = install('0000abcd-0000-1000-8000-00805f9b34fb');

    await expectAsync(new BlePipe(line => lines.push(line)).open())
      .toBeRejectedWithError(/Aucun service Supvan connu/);
    expect(fake.server.disconnect).toHaveBeenCalled();
  });
});
