import {
  ScanOutboxEntry,
  ScanOutboxStatus,
  ScanSetAsideReason,
} from './scan-offline.model';

export type LegacyScanOutboxEntry = Omit<ScanOutboxEntry, 'status' | 'clientSessionId'> & {
  clientSessionId?: string;
  scanSessionId?: string;
  status: ScanOutboxStatus | 'Orphaned' | 'Quarantined';
};

export function migrateLegacyScanOutboxEntry(entry: LegacyScanOutboxEntry): ScanOutboxEntry {
  const clientSessionId = entry.clientSessionId ?? entry.scanSessionId;
  if (!clientSessionId) {
    throw new Error(`Cannot migrate scan gesture without a session: ${entry.clientGestureId}`);
  }

  if (entry.status === 'Orphaned') {
    const {status: _status, scanSessionId: _scanSessionId, clientSessionId: _clientSessionId, ...rest} = entry;
    return {
      ...rest,
      clientSessionId,
      status: entry.kept === null ? 'NeedsDecision' : 'NeedsReattach',
      setAsideReason: entry.kept === null ? 'undecided' : 'no-session',
    };
  }

  if (entry.status === 'Quarantined') {
    const {status: _status, scanSessionId: _scanSessionId, clientSessionId: _clientSessionId, ...rest} = entry;
    return {
      ...rest,
      clientSessionId,
      status: 'RejectedByServer',
      setAsideReason: 'server-refused',
    };
  }

  const reason: ScanSetAsideReason | null = entry.setAsideReason ?? null;
  const {status, scanSessionId: _scanSessionId, clientSessionId: _clientSessionId, ...rest} = entry;
  return {
    ...rest,
    clientSessionId,
    status,
    setAsideReason: reason,
  };
}
