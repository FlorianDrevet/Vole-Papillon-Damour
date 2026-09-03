import type {ScanEnvironment} from './environment.model';
import {localApiUrlForHost} from './local-api-url';

const localHost = typeof window === 'undefined' ? 'localhost' : window.location.hostname;

export const environment: ScanEnvironment = {
  production: false,
  apiUrl: localApiUrlForHost(localHost),
};
