import {localApiUrlForHost} from './local-api-url';

describe('localApiUrlForHost', () => {
  it('uses the host serving the scan app for a phone on the local network', () => {
    expect(localApiUrlForHost('192.168.1.42')).toBe('http://192.168.1.42:5257');
  });

  it('falls back to localhost when no host is available', () => {
    expect(localApiUrlForHost('')).toBe('http://localhost:5257');
  });
});
